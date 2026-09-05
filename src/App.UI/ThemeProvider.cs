using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using App.Infrastructure.Configuration;
using ModernWpf;
using Newtonsoft.Json;

namespace FldrSrtr
{
    /// <summary>
    /// Themes: three built-ins ("Systeem"/"Licht"/"Donker", handled in code, no files needed) plus
    /// any number of custom themes a user creates via ThemeEditorWindow — each saved as a small
    /// JSON file "Themes/&lt;Name&gt;.json" next to the exe (same portable, no-rebuild-needed
    /// philosophy as IconSetProvider/Localization). A custom theme is just a base (Light/Dark/
    /// follow system) plus an optional accent color. Applied via ModernWpf's ThemeManager, which
    /// re-themes every open window live — no restart needed, unlike IconSet/Language.
    /// </summary>
    public static class ThemeProvider
    {
        public const string SystemThemeName = "Systeem";
        public const string LightThemeName = "Licht";
        public const string DarkThemeName = "Donker";

        private static readonly string[] BuiltInNames = { SystemThemeName, LightThemeName, DarkThemeName };

        public static string ThemesRootFolder => Path.Combine(PortablePaths.BaseDirectory, "Themes");

        public static void ApplySetting(string themeName)
        {
            (ApplicationTheme? baseTheme, string accentHex) = Resolve(themeName);
            Apply(baseTheme, accentHex);
        }

        /// <summary>Applies a base/accent combination directly, without needing a saved theme
        /// name yet — used by ThemeEditorWindow for live preview while the user is still picking.</summary>
        public static void Apply(ApplicationTheme? baseTheme, string accentHex)
        {
            ThemeManager.Current.ApplicationTheme = baseTheme;
            ThemeManager.Current.AccentColor = string.IsNullOrEmpty(accentHex)
                ? (System.Windows.Media.Color?)null
                : (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(accentHex);
        }

        private static (ApplicationTheme?, string) Resolve(string themeName)
        {
            if (string.Equals(themeName, LightThemeName, StringComparison.OrdinalIgnoreCase))
            {
                return (ApplicationTheme.Light, null);
            }
            if (string.Equals(themeName, DarkThemeName, StringComparison.OrdinalIgnoreCase))
            {
                return (ApplicationTheme.Dark, null);
            }
            if (string.Equals(themeName, SystemThemeName, StringComparison.OrdinalIgnoreCase))
            {
                return (null, null);
            }

            ThemeFile file = LoadThemeFile(themeName);
            if (file == null)
            {
                return (null, null); // unknown/missing custom theme: fall back to following the OS
            }

            ApplicationTheme? based = string.Equals(file.Base, "Light", StringComparison.OrdinalIgnoreCase)
                ? ApplicationTheme.Light
                : string.Equals(file.Base, "Dark", StringComparison.OrdinalIgnoreCase)
                    ? ApplicationTheme.Dark
                    : (ApplicationTheme?)null;
            return (based, file.AccentColor);
        }

        private static ThemeFile LoadThemeFile(string themeName)
        {
            string path = Path.Combine(ThemesRootFolder, themeName + ".json");
            if (!File.Exists(path))
            {
                return null;
            }

            try
            {
                return JsonConvert.DeserializeObject<ThemeFile>(File.ReadAllText(path));
            }
            catch (JsonException)
            {
                return null; // corrupt theme file: fall back rather than crash
            }
        }

        /// <summary>Built-in names first, then custom themes found on disk (alphabetical, and
        /// never duplicating a built-in name even if a stray file happens to match one).</summary>
        public static string[] GetAvailableThemes()
        {
            IEnumerable<string> custom = Directory.Exists(ThemesRootFolder)
                ? Directory.GetFiles(ThemesRootFolder, "*.json")
                    .Select(Path.GetFileNameWithoutExtension)
                    .Where(name => !BuiltInNames.Contains(name, StringComparer.OrdinalIgnoreCase))
                    .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
                : Enumerable.Empty<string>();

            return BuiltInNames.Concat(custom).ToArray();
        }

        /// <summary>Saves the current base/accent as a new named custom theme, immediately usable
        /// from the Settings dropdown.</summary>
        public static void SaveCustomTheme(string themeName, ApplicationTheme? baseTheme, string accentHex)
        {
            Directory.CreateDirectory(ThemesRootFolder);
            var file = new ThemeFile
            {
                Base = baseTheme == ApplicationTheme.Light ? "Light" : baseTheme == ApplicationTheme.Dark ? "Dark" : "System",
                AccentColor = accentHex
            };
            File.WriteAllText(Path.Combine(ThemesRootFolder, themeName + ".json"), JsonConvert.SerializeObject(file, Formatting.Indented));
        }

        private class ThemeFile
        {
            public string Base { get; set; }
            public string AccentColor { get; set; }
        }
    }
}
