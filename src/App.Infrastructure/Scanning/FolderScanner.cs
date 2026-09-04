using System;
using System.Collections.Generic;
using System.IO.Abstractions;
using System.Linq;
using App.Core.Evaluation;
using App.Core.Model;

namespace App.Infrastructure.Scanning
{
    /// <summary>
    /// On-demand folder scan. Non-recursive by default (Fase 1); WatchedFolder.Recursive opts
    /// into depth-limited recursion with excluded subfolders/file patterns (Fase 2).
    /// </summary>
    public class FolderScanner
    {
        private readonly IFileSystem _fileSystem;

        public FolderScanner(IFileSystem fileSystem)
        {
            _fileSystem = fileSystem;
        }

        /// <summary>Simple non-recursive scan — kept for callers that don't need a WatchedFolder's settings.</summary>
        public IReadOnlyList<FileEntry> Scan(string folderPath)
        {
            return Scan(new WatchedFolder { Path = folderPath, Recursive = false });
        }

        public IReadOnlyList<FileEntry> Scan(WatchedFolder folder)
        {
            var results = new List<FileEntry>();
            ScanRecursive(folder.Path, folder, depth: 0, results);
            return results;
        }

        private void ScanRecursive(string folderPath, WatchedFolder folder, int depth, List<FileEntry> results)
        {
            if (!_fileSystem.Directory.Exists(folderPath))
            {
                return;
            }

            foreach (string filePath in _fileSystem.Directory.EnumerateFiles(folderPath))
            {
                IFileInfo info = _fileSystem.FileInfo.New(filePath);
                if (ExclusionMatcher.IsFileExcluded(info.Name, folder.ExcludedFilePatterns))
                {
                    continue;
                }

                results.Add(ToFileEntry(info));
            }

            if (!folder.Recursive || depth >= folder.MaxRecursionDepth)
            {
                return;
            }

            foreach (string subDirPath in _fileSystem.Directory.EnumerateDirectories(folderPath))
            {
                string subDirName = _fileSystem.Path.GetFileName(subDirPath);
                if (ExclusionMatcher.IsSubfolderExcluded(subDirName, folder.ExcludedSubfolders))
                {
                    continue;
                }

                ScanRecursive(subDirPath, folder, depth + 1, results);
            }
        }

        private static FileEntry ToFileEntry(IFileInfo info) => new FileEntry
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
