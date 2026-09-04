using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace App.Core.Configuration
{
    /// <summary>
    /// User-configurable safety knobs (§3.8). The hardcoded Windows roots in ProtectedPathGuard
    /// always apply on top of these — this only adds extra protection, never removes the baseline.
    /// </summary>
    public class AppSettings
    {
        public ObservableCollection<string> ProtectedFolders { get; set; } = new ObservableCollection<string>();
        public ObservableCollection<string> ProtectedExtensions { get; set; } = new ObservableCollection<string>();

        /// <summary>Ask for extra confirmation when a real run would touch more files than this.</summary>
        public int ConfirmationThreshold { get; set; } = 20;

        /// <summary>Hard cap — a real run matching more files than this is refused outright.</summary>
        public int MaxFilesPerRun { get; set; } = 1000;

        /// <summary>Which icon pack (a folder under IconSets\) to use — see IconSetProvider. Applied live when changed in Settings.</summary>
        public string IconSet { get; set; } = "Default";

        /// <summary>
        /// Whether saving Settings should back up the previous general.config.json first.
        /// Folders/rules (folders.config.json) are always backed up regardless of this — this
        /// only controls the noisier "I'm just tweaking a setting" case some users don't want a
        /// backup file for every time.
        /// </summary>
        public bool BackupOnSettingsChange { get; set; } = true;

        /// <summary>
        /// Per-icon overrides on top of the active IconSet: key is an icon filename (e.g.
        /// "icon-add.png", matching the Default pack's naming), value is an absolute path to a
        /// PNG the user picked to replace it. Lets someone swap a handful of icons without
        /// building a whole pack folder. Checked first by IconPathConverter, before the active
        /// pack and before the Default fallback. Applied live when changed in Settings.
        /// </summary>
        public Dictionary<string, string> IconOverrides { get; set; } = new Dictionary<string, string>();
    }
}
