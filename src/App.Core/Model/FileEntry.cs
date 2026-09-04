using System;

namespace App.Core.Model
{
    /// <summary>
    /// Plain snapshot of a file's metadata. Core never touches disk directly — Infrastructure
    /// populates this via a scanner, so condition evaluation stays pure and unit-testable.
    /// </summary>
    public class FileEntry
    {
        public string FullPath { get; set; }
        public string Directory { get; set; }
        public string Name { get; set; }

        /// <summary>Without the leading dot, e.g. "pdf". Empty string for extensionless files.</summary>
        public string Extension { get; set; }

        public long SizeBytes { get; set; }
        public DateTime CreatedUtc { get; set; }
        public DateTime ModifiedUtc { get; set; }
        public DateTime AccessedUtc { get; set; }

        /// <summary>
        /// Set by DuplicateDetector before evaluation, only when a rule actually checks it —
        /// hashing every scanned file "just in case" would violate §4.1's performance guidance.
        /// </summary>
        public bool IsDuplicate { get; set; }
    }
}
