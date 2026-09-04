using Ookii.Dialogs.WinForms;

namespace FldrSrtr
{
    /// <summary>
    /// Wraps Ookii.Dialogs.WinForms' VistaFolderBrowserDialog — the real modern Explorer-style
    /// folder picker (address bar, can type/paste a path, favorites/recents), unlike the classic
    /// System.Windows.Forms.FolderBrowserDialog which .NET Framework never upgraded past the old
    /// SHBrowseForFolder UI. Falls back automatically to the classic dialog on older Windows.
    /// </summary>
    public static class ModernFolderPicker
    {
        public static string PickFolder(string description = "Selecteer een map", string initialPath = null)
        {
            var dialog = new VistaFolderBrowserDialog
            {
                Description = description,
                UseDescriptionForTitle = true,
                SelectedPath = initialPath
            };

            return dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK ? dialog.SelectedPath : null;
        }
    }
}
