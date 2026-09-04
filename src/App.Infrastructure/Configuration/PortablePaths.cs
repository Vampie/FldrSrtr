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

        public static string ConfigFilePath => Path.Combine(BaseDirectory, "config.json");

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
