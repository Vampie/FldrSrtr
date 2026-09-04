namespace FldrSrtr
{
    /// <summary>
    /// Holds which icon set's folder is currently active. Set once at startup from
    /// AppConfig.Settings.IconSet — changing the setting takes effect on next launch rather than
    /// live-refreshing every already-open window's icons, which keeps this trivial.
    /// </summary>
    public static class IconSetProvider
    {
        public const string DefaultFolder = "Icons/";
        public const string SlimFolder = "Icons/Slim/";

        public static string BasePath { get; set; } = DefaultFolder;

        public static void ApplySetting(string iconSetName)
        {
            BasePath = iconSetName == "Slim" ? SlimFolder : DefaultFolder;
        }
    }
}
