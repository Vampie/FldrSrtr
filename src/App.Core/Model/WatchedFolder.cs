using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace App.Core.Model
{
    /// <summary>Raises PropertyChanged so a bound Folders list entry (path, enabled checkbox)
    /// updates live as the user edits it — e.g. via Folder settings' path textbox.</summary>
    public class WatchedFolder : INotifyPropertyChanged
    {
        private string _path;
        private bool _enabled = true;
        private bool _recursive;
        private int _maxRecursionDepth = 10;

        public string Id { get; set; } = Guid.NewGuid().ToString("N");

        public string Path
        {
            get => _path;
            set => SetField(ref _path, value);
        }

        public bool Enabled
        {
            get => _enabled;
            set => SetField(ref _enabled, value);
        }

        public bool Recursive
        {
            get => _recursive;
            set => SetField(ref _recursive, value);
        }

        /// <summary>Only used when Recursive is true. 0 = top-level folder only.</summary>
        public int MaxRecursionDepth
        {
            get => _maxRecursionDepth;
            set => SetField(ref _maxRecursionDepth, value);
        }

        /// <summary>Wildcard patterns matched against file name, e.g. "*.tmp", "*.part", "~*".</summary>
        public ObservableCollection<string> ExcludedFilePatterns { get; set; } = new ObservableCollection<string>();

        /// <summary>Subfolder names skipped entirely during recursion, e.g. "node_modules", ".git".</summary>
        public ObservableCollection<string> ExcludedSubfolders { get; set; } = new ObservableCollection<string>();

        public ObservableCollection<Rule> Rules { get; set; } = new ObservableCollection<Rule>();

        public event PropertyChangedEventHandler PropertyChanged;

        private void SetField<T>(ref T field, T value, [CallerMemberName] string propertyName = null)
        {
            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
