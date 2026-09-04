using System.Collections.ObjectModel;
using App.Core.Model;

namespace App.Core.Configuration
{
    /// <summary>
    /// Root of the portable configuration file. Grows across phases; SchemaVersion exists
    /// from Fase 0 so later migrations have something to key off.
    /// </summary>
    public class AppConfig
    {
        public int SchemaVersion { get; set; } = 1;
        public ObservableCollection<WatchedFolder> Folders { get; set; } = new ObservableCollection<WatchedFolder>();

        public static AppConfig CreateDefault()
        {
            return new AppConfig
            {
                SchemaVersion = 1
            };
        }
    }
}
