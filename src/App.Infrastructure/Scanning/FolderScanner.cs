using System;
using System.Collections.Generic;
using System.IO.Abstractions;
using System.Linq;
using App.Core.Model;

namespace App.Infrastructure.Scanning
{
    /// <summary>
    /// On-demand, non-recursive folder scan (Fase 1 — recursion arrives in Fase 2). Uses
    /// System.IO.Abstractions so this is testable against a MockFileSystem, never the real disk.
    /// </summary>
    public class FolderScanner
    {
        private readonly IFileSystem _fileSystem;

        public FolderScanner(IFileSystem fileSystem)
        {
            _fileSystem = fileSystem;
        }

        public IReadOnlyList<FileEntry> Scan(string folderPath)
        {
            if (!_fileSystem.Directory.Exists(folderPath))
            {
                return Array.Empty<FileEntry>();
            }

            return _fileSystem.Directory.EnumerateFiles(folderPath)
                .Select(ToFileEntry)
                .ToList();
        }

        private FileEntry ToFileEntry(string filePath)
        {
            IFileInfo info = _fileSystem.FileInfo.New(filePath);
            return new FileEntry
            {
                FullPath = info.FullName,
                Directory = info.DirectoryName,
                Name = info.Name,
                Extension = info.Extension.TrimStart('.'),
                SizeBytes = info.Length,
                CreatedUtc = info.CreationTimeUtc,
                ModifiedUtc = info.LastWriteTimeUtc,
                AccessedUtc = info.LastAccessTimeUtc
            };
        }
    }
}
