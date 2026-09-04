using System.Collections.ObjectModel;
using App.Core.Model;

namespace App.Core.Configuration
{
    public static class SchemaVersions
    {
        /// <summary>Fase 4: Rule.Conditions/Logic (flat list) became Rule.RootCondition (tree) back in Fase 2 — see ConfigMigrator.</summary>
        public const int Current = 2;
    }

    /// <summary>
    /// Root of the portable configuration file. Grows across phases; SchemaVersion exists
    /// from Fase 0 so later migrations have something to key off.
    /// </summary>
    public class AppConfig
    {
        public int SchemaVersion { get; set; } = SchemaVersions.Current;
        public ObservableCollection<WatchedFolder> Folders { get; set; } = new ObservableCollection<WatchedFolder>();
        public AppSettings Settings { get; set; } = new AppSettings();

        public static AppConfig CreateDefault()
        {
            return new AppConfig
            {
                SchemaVersion = SchemaVersions.Current
            };
        }
    }
}
