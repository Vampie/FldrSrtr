using System;
using System.IO;

namespace App.Infrastructure.Configuration
{
    /// <summary>
    /// Resolves every on-disk location relative to the application's own folder.
    /// Never touch %AppData% or the registry here — the app must stay portable.
    /// </summary>
    public static class PortablePaths
    {
        public static string BaseDirectory => AppDomain.CurrentDomain.BaseDirectory;

        /// <summary>Pre-split single-file config, kept only so ConfigService can detect and
        /// migrate it on first load after upgrading. New installs never write this file.</summary>
        public static string LegacyConfigFilePath => Path.Combine(BaseDirectory, "config.json");

        /// <summary>App-wide settings (§3.8 safety knobs, icon set, ...) — the "Settings" half of
        /// the old combined config.json.</summary>
        public static string GeneralConfigFilePath => Path.Combine(BaseDirectory, "general.config.json");

        /// <summary>Watched folders and their rules — the "Folders" half of the old combined
        /// config.json.</summary>
        public static string FoldersConfigFilePath => Path.Combine(BaseDirectory, "folders.config.json");

        /// <summary>
        /// Throws a clear, actionable exception instead of a bare UnauthorizedAccessException
        /// when the app's own folder can't be written to (e.g. Program Files without elevation).
        /// </summary>
        public static void EnsureBaseDirectoryIsWritable()
        {
            string probePath = Path.Combine(BaseDirectory, $".write-check-{Guid.NewGuid():N}.tmp");
            try
            {
                File.WriteAllText(probePath, string.Empty);
                File.Delete(probePath);
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException || ex is IOException)
            {
                throw new InvalidOperationException(
                    $"FldrSrtr kan niet schrijven naar zijn eigen map:\n{BaseDirectory}\n\n" +
                    "FldrSrtr is portable en heeft schrijfrechten nodig in de map waarin het staat " +
                    "(voor config.json en het activity-log). Verplaats de map naar een locatie waar je " +
                    "schrijfrechten hebt, bijvoorbeeld je Documenten-map, en start opnieuw.",
                    ex);
            }
        }
    }
}
