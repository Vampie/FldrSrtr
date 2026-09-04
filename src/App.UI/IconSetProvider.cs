namespace FldrSrtr
{
    /// <summary>
    /// Holds which icon set's folder is currently active. Set at startup from
    /// AppConfig.Settings.IconSet, and updated live when the user changes it in Settings — see
    /// MainWindow.RefreshIconsLive, which re-runs IconPathConverter for every icon already on
    /// screen instead of requiring a restart.
    /// </summary>
    public static class IconSetProvider
    {
        public const string DefaultFolder = "Icons/";
        public const string SlimFolder = "Icons/Slim/";
        public const string LineFolder = "Icons/Line/";

        public static string BasePath { get; set; } = DefaultFolder;

        public static void ApplySetting(string iconSetName)
        {
            switch (iconSetName)
            {
                case "Slim":
                    BasePath = SlimFolder;
                    break;
                case "Line":
                    BasePath = LineFolder;
                    break;
                default:
                    BasePath = DefaultFolder;
                    break;
            }
        }
    }
}
