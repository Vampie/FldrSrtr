using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using App.Infrastructure.Configuration;
using Newtonsoft.Json;

namespace FldrSrtr
{
    /// <summary>
    /// Icon packs live on disk under "IconSets/&lt;PackName&gt;/", next to the exe (§4.5 portable
    /// rule — never embedded, so a pack can be added or edited without rebuilding). Each pack
    /// folder has an "icons.json" manifest — a {key: filename} map, key being the canonical icon
    /// name buttons use (e.g. "icon-add.png") and filename being whatever the actual PNG in that
    /// folder is called. This is what lets IconOverridesWindow "Bewaren als set..." build a pack
    /// out of icons that came from anywhere, under their own original names, without renaming
    /// them to match a fixed convention. A pack with no manifest (e.g. a hand-made folder someone
    /// drops in) falls back to treating filename == key, so the plain "copy the Default set's
    /// filenames" approach still works too.
    /// FldrSrtr ships with a few packs there (Default, Slim, Line, Chanut, Sharp), each with an
    /// identity manifest (every key maps to itself); anyone can add a subfolder and it shows up in
    /// Settings, or override individual icons — IconPathConverter falls back to Default for any
    /// icon a pack's manifest doesn't cover, so a partial custom pack still works.
    /// Active pack is set at startup from AppConfig.Settings.IconSet, and updated live when the
    /// user changes it in Settings — see MainWindow.TryRefreshIconsLive, which re-runs
    /// IconPathConverter for every icon already on screen instead of requiring a restart.
    /// </summary>
    public static class IconSetProvider
    {
        public const string DefaultSetName = "Default";
        public const string ManifestFileName = "icons.json";

        private static Dictionary<string, string> _activeManifest = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private static Dictionary<string, string> _defaultManifest;

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
            _activeManifest = LoadManifest(BasePath);
        }

        public static void ApplyOverrides(IDictionary<string, string> overrides)
        {
            Overrides = overrides != null
                ? new Dictionary<string, string>(overrides, StringComparer.OrdinalIgnoreCase)
                : new Dictionary<string, string>();
        }

        /// <summary>Resolves an icon key to the actual filename inside a pack folder — the
        /// manifest's mapping if the pack has one and knows this key, otherwise the key itself
        /// (the filename == key convention every built-in pack also happens to satisfy).</summary>
        public static string ResolveFileName(string packFolder, string key)
        {
            Dictionary<string, string> manifest = string.Equals(packFolder, BasePath, StringComparison.OrdinalIgnoreCase)
                ? _activeManifest
                : string.Equals(packFolder, DefaultSetFolder, StringComparison.OrdinalIgnoreCase)
                    ? (_defaultManifest ?? (_defaultManifest = LoadManifest(DefaultSetFolder)))
                    : LoadManifest(packFolder);

            return manifest != null && manifest.TryGetValue(key, out string fileName) ? fileName : key;
        }

        private static Dictionary<string, string> LoadManifest(string packFolder)
        {
            string manifestPath = Path.Combine(packFolder, ManifestFileName);
            if (!File.Exists(manifestPath))
            {
                return null;
            }

            try
            {
                return JsonConvert.DeserializeObject<Dictionary<string, string>>(File.ReadAllText(manifestPath))
                       ?? new Dictionary<string, string>();
            }
            catch (JsonException)
            {
                return null; // corrupt manifest: fall back to the filename == key convention rather than breaking every icon in this pack
            }
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

        /// <summary>
        /// Builds a brand new pack under IconSets\packName\: copies each supplied (key, sourceFile)
        /// icon into the folder under its key name, then writes an identity manifest — so the pack
        /// is immediately usable both by this code (manifest present) and by anyone poking around
        /// the folder by hand (filenames still match the plain convention too).
        /// </summary>
        public static void SavePack(string packName, IReadOnlyDictionary<string, string> keyToSourceFile)
        {
            string packFolder = Path.Combine(IconSetsRootFolder, packName);
            Directory.CreateDirectory(packFolder);

            var manifest = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (KeyValuePair<string, string> entry in keyToSourceFile)
            {
                if (string.IsNullOrEmpty(entry.Value) || !File.Exists(entry.Value))
                {
                    continue;
                }

                File.Copy(entry.Value, Path.Combine(packFolder, entry.Key), overwrite: true);
                manifest[entry.Key] = entry.Key;
            }

            File.WriteAllText(Path.Combine(packFolder, ManifestFileName), JsonConvert.SerializeObject(manifest, Formatting.Indented));
        }
    }
}
