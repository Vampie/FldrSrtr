using System;
using System.IO.Abstractions;
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

            _fileSystem.File.Copy(sourcePath, destinationPath, overwrite: true);
        }

        public void DeleteToRecycleBin(string path)
        {
            GuardAgainstProtectedPath(path);

            Microsoft.VisualBasic.FileIO.FileSystem.DeleteFile(
                path,
                Microsoft.VisualBasic.FileIO.UIOption.OnlyErrorDialogs,
                Microsoft.VisualBasic.FileIO.RecycleOption.SendToRecycleBin);
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
