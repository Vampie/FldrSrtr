namespace App.Core.Configuration
{
    /// <summary>
    /// Root of the portable configuration file. Grows across phases (folders, rules, ...);
    /// Fase 0 only needs the schema version so migrations have something to key off later.
    /// </summary>
    public class AppConfig
    {
        public int SchemaVersion { get; set; } = 1;

        public static AppConfig CreateDefault()
        {
            return new AppConfig
            {
                SchemaVersion = 1
            };
        }
    }
}
