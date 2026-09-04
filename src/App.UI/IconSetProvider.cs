using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using App.Infrastructure.Configuration;

namespace FldrSrtr
{
    /// <summary>
    /// Icon packs live on disk under "IconSets/&lt;PackName&gt;/icon-*.png", next to the exe (§4.5
    /// portable rule — never embedded, so a pack can be added or edited without rebuilding).
    /// FldrSrtr ships with a few packs there (Default, Slim, Line, Chanut, Sharp); anyone can drop
    /// in a new subfolder using the Default set's filenames and it shows up in Settings, or
    /// override individual icons in an existing pack — IconPathConverter falls back to Default for
    /// any file a pack doesn't provide, so a partial custom pack still works.
    /// Active pack is set at startup from AppConfig.Settings.IconSet, and updated live when the
    /// user changes it in Settings — see MainWindow.TryRefreshIconsLive, which re-runs
    /// IconPathConverter for every icon already on screen instead of requiring a restart.
    /// </summary>
    public static class IconSetProvider
    {
        public const string DefaultSetName = "Default";

        public static string IconSetsRootFolder => Path.Combine(PortablePaths.BaseDirectory, "IconSets");

        public static string DefaultSetFolder => Path.Combine(IconSetsRootFolder, DefaultSetName);

        public static string BasePath { get; private set; } = DefaultSetFolder;

        /// <summary>Per-icon overrides on top of BasePath — see AppSettings.IconOverrides.
        /// Key: icon filename (e.g. "icon-add.png"); value: absolute path to the replacement PNG.</summary>
        public static IReadOnlyDictionary<string, string> Overrides { get; private set; } = new Dictionary<string, string>();

        public static void ApplySetting(string iconSetName)
        {
            string candidate = Path.Combine(IconSetsRootFolder, string.IsNullOrWhiteSpace(iconSetName) ? DefaultSetName : iconSetName);
            BasePath = Directory.Exists(candidate) ? candidate : DefaultSetFolder;
        }

        public static void ApplyOverrides(IDictionary<string, string> overrides)
        {
            Overrides = overrides != null
                ? new Dictionary<string, string>(overrides, StringComparer.OrdinalIgnoreCase)
                : new Dictionary<string, string>();
        }

        /// <summary>Every pack folder found under IconSets — a plain directory listing, so a
        /// user-added pack appears without any code change. Falls back to just "Default" if the
        /// IconSets folder itself is missing (e.g. deleted by hand) so Settings never shows an
        /// empty list.</summary>
        public static string[] GetAvailableIconSets()
        {
            if (!Directory.Exists(IconSetsRootFolder))
            {
                return new[] { DefaultSetName };
            }

            string[] names = Directory.GetDirectories(IconSetsRootFolder)
                .Select(Path.GetFileName)
                .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            return names.Length > 0 ? names : new[] { DefaultSetName };
        }

        /// <summary>Every icon key the app can show, derived from the Default pack's own files —
        /// the single source of truth for "which icons exist", so a new default icon added later
        /// shows up here automatically.</summary>
        public static string[] GetAllIconKeys()
        {
            if (!Directory.Exists(DefaultSetFolder))
            {
                return new string[0];
            }

            return Directory.GetFiles(DefaultSetFolder, "*.png")
                .Select(Path.GetFileName)
                .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }
    }
}
