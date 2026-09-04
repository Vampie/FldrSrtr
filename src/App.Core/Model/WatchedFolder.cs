using System;
using System.Collections.ObjectModel;

namespace App.Core.Model
{
    public class WatchedFolder
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        public string Path { get; set; }
        public bool Enabled { get; set; } = true;

        public bool Recursive { get; set; } = false;

        /// <summary>Only used when Recursive is true. 0 = top-level folder only.</summary>
        public int MaxRecursionDepth { get; set; } = 10;

        /// <summary>Wildcard patterns matched against file name, e.g. "*.tmp", "*.part", "~*".</summary>
        public ObservableCollection<string> ExcludedFilePatterns { get; set; } = new ObservableCollection<string>();

        /// <summary>Subfolder names skipped entirely during recursion, e.g. "node_modules", ".git".</summary>
        public ObservableCollection<string> ExcludedSubfolders { get; set; } = new ObservableCollection<string>();

        public ObservableCollection<Rule> Rules { get; set; } = new ObservableCollection<Rule>();
    }
}
