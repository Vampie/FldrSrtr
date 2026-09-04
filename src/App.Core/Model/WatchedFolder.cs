using System;
using System.Collections.ObjectModel;

namespace App.Core.Model
{
    public class WatchedFolder
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        public string Path { get; set; }
        public bool Enabled { get; set; } = true;

        /// <summary>Recursive scanning lands in Fase 2 — the flag exists now so config is forward-compatible.</summary>
        public bool Recursive { get; set; } = false;

        public ObservableCollection<Rule> Rules { get; set; } = new ObservableCollection<Rule>();
    }
}
