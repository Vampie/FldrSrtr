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
    }
}
