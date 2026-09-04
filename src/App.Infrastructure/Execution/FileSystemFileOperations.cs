using System;
using System.Diagnostics;
using System.IO.Abstractions;
using System.IO.Compression;
using System.Security.Cryptography;
using App.Core.Execution;
using App.Infrastructure.Safety;

namespace App.Infrastructure.Execution
{
    /// <summary>
    /// Real disk implementation of Core's IFileOperations. Guards every mutating call against
    /// the hardcoded protected roots (§3.8) and uses Microsoft.VisualBasic's FileSystem for
    /// Recycle Bin delete — built into .NET Framework, no extra dependency needed.
    /// </summary>
    public class FileSystemFileOperations : IFileOperations
    {
        private readonly IFileSystem _fileSystem;
        private readonly ProtectedPathGuard _guard;

        public FileSystemFileOperations(IFileSystem fileSystem, ProtectedPathGuard guard)
        {
            _fileSystem = fileSystem;
            _guard = guard;
        }

        public bool FileExists(string path) => _fileSystem.File.Exists(path);

        public void Move(string sourcePath, string destinationPath)
        {
            GuardAgainstProtectedPath(sourcePath);
            GuardAgainstProtectedPath(destinationPath);
            EnsureDestinationDirectoryExists(destinationPath);

            if (_fileSystem.File.Exists(destinationPath))
            {
                _fileSystem.File.Delete(destinationPath);
            }

            _fileSystem.File.Move(sourcePath, destinationPath);
        }

        public void Copy(string sourcePath, string destinationPath)
        {
            GuardAgainstProtectedPath(sourcePath);
            GuardAgainstProtectedPath(destinationPath);
            EnsureDestinationDirectoryExists(destinationPath);

            _fileSystem.File.Copy(sourcePath, destinationPath, overwrite: true);
        }

        /// <summary>
        /// Move/Copy destinations can now contain dynamic variables like {Year}\{Month} (§3.6) —
        /// nobody pre-creates every year/month folder, so the target directory must be created
        /// on demand rather than requiring the destination folder to already exist.
        /// </summary>
        private void EnsureDestinationDirectoryExists(string destinationPath)
        {
            string directory = _fileSystem.Path.GetDirectoryName(destinationPath);
            if (!string.IsNullOrEmpty(directory) && !_fileSystem.Directory.Exists(directory))
            {
                _fileSystem.Directory.CreateDirectory(directory);
            }
        }

        public void DeleteToRecycleBin(string path)
        {
            GuardAgainstProtectedPath(path);

            Microsoft.VisualBasic.FileIO.FileSystem.DeleteFile(
                path,
                Microsoft.VisualBasic.FileIO.UIOption.OnlyErrorDialogs,
                Microsoft.VisualBasic.FileIO.RecycleOption.SendToRecycleBin);
        }

        public string ComputeSha256(string path)
        {
            using (var sha256 = SHA256.Create())
            using (var stream = _fileSystem.File.OpenRead(path))
            {
                byte[] hash = sha256.ComputeHash(stream);
                return BitConverter.ToString(hash).Replace("-", string.Empty).ToLowerInvariant();
            }
        }

        public void CreateDirectory(string path)
        {
            GuardAgainstProtectedPath(path);
            _fileSystem.Directory.CreateDirectory(path);
        }

        /// <summary>
        /// System.IO.Abstractions has no compression wrapper, so this goes straight to the real
        /// filesystem — acceptable since it's a thin, well-understood BCL call.
        /// </summary>
        public void AddToZip(string filePath, string zipPath)
        {
            GuardAgainstProtectedPath(filePath);
            GuardAgainstProtectedPath(zipPath);
            EnsureDestinationDirectoryExists(zipPath);

            ZipArchiveMode mode = System.IO.File.Exists(zipPath) ? ZipArchiveMode.Update : ZipArchiveMode.Create;
            using (var archive = ZipFile.Open(zipPath, mode))
            {
                archive.CreateEntryFromFile(filePath, System.IO.Path.GetFileName(filePath));
            }
        }

        public void OpenFile(string path)
        {
            GuardAgainstProtectedPath(path);
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
        }

        public void OpenFileWith(string applicationPath, string filePath)
        {
            GuardAgainstProtectedPath(filePath);
            Process.Start(applicationPath, $"\"{filePath}\"");
        }

        public void ExecuteExternal(string executablePath, string arguments)
        {
            Process.Start(new ProcessStartInfo(executablePath, arguments ?? string.Empty) { UseShellExecute = false });
        }

        private void GuardAgainstProtectedPath(string path)
        {
            if (_guard.IsProtected(path))
            {
                throw new InvalidOperationException($"Actie geweigerd: '{path}' valt onder een beschermde map.");
            }
        }
    }
}
