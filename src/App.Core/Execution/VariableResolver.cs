using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using App.Core.Model;

namespace App.Core.Execution
{
    /// <summary>
    /// Resolves {FileName}, {Year}, {CreatedYear}, {Counter}, ... tokens in an action's
    /// Destination/Arguments (§3.6). Tokens are matched case-insensitively — {filename} and
    /// {FILENAME} both work.
    /// {FileName}/{Extension} reflect the file's *current* location in the action chain (so a
    /// rename earlier in the same rule is visible to a later action); {OriginalName} /
    /// {OriginalExtension} / {FileSize} reflect the file as it was when the folder was scanned.
    /// {Year}/{Month}/... use the moment the rule runs; {CreatedYear}/{ModifiedYear}/... use the
    /// file's own Created/Modified timestamps instead.
    /// {Counter}/{Counter:start}/{Counter:start:step} is resolved via the counterResolver
    /// callback — RuleEngine supplies one that's fresh per run (never persisted) and consistent
    /// across every action applied to the same file.
    /// </summary>
    public static class VariableResolver
    {
        private static readonly Regex TokenPattern = new Regex(@"\{([^{}]+)\}", RegexOptions.Compiled);

        public static string Resolve(string template, FileEntry originalFile, string currentPath, DateTime now, Func<string, int> counterResolver = null)
        {
            if (string.IsNullOrEmpty(template))
            {
                return template;
            }

            var tokens = BuildTokenMap(originalFile, currentPath, now);

            return TokenPattern.Replace(template, match =>
            {
                string spec = match.Groups[1].Value;

                if (IsCounterSpec(spec))
                {
                    return counterResolver != null ? counterResolver(spec).ToString(CultureInfo.InvariantCulture) : match.Value;
                }

                return tokens.TryGetValue(spec, out string value) ? value : match.Value;
            });
        }

        private static bool IsCounterSpec(string spec) =>
            spec.Equals("Counter", StringComparison.OrdinalIgnoreCase) ||
            spec.StartsWith("Counter:", StringComparison.OrdinalIgnoreCase);

        /// <summary>Parses "Counter", "Counter:100" or "Counter:100:5" into (start, step), defaulting to (1, 1).</summary>
        public static void ParseCounterSpec(string spec, out int start, out int step)
        {
            start = 1;
            step = 1;

            string[] parts = (spec ?? string.Empty).Split(':');
            if (parts.Length >= 2 && int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsedStart))
            {
                start = parsedStart;
            }
            if (parts.Length >= 3 && int.TryParse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsedStep))
            {
                step = parsedStep;
            }
        }

        private static Dictionary<string, string> BuildTokenMap(FileEntry originalFile, string currentPath, DateTime now)
        {
            string currentName = Path.GetFileNameWithoutExtension(currentPath);
            string currentExtension = Path.GetExtension(currentPath).TrimStart('.');
            string originalName = Path.GetFileNameWithoutExtension(originalFile.Name);

            var tokens = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["FileName"] = currentName,
                ["OriginalName"] = originalName,
                ["Extension"] = currentExtension,
                ["OriginalExtension"] = originalFile.Extension,
                ["FullPath"] = currentPath,
                ["Directory"] = Path.GetDirectoryName(currentPath) ?? string.Empty,
                ["FileSize"] = originalFile.SizeBytes.ToString()
            };

            AddDateTokens(tokens, string.Empty, now);
            AddDateTokens(tokens, "Created", originalFile.CreatedUtc.ToLocalTime());
            AddDateTokens(tokens, "Modified", originalFile.ModifiedUtc.ToLocalTime());

            return tokens;
        }

        private static void AddDateTokens(Dictionary<string, string> tokens, string prefix, DateTime moment)
        {
            tokens[$"{prefix}Year"] = moment.ToString("yyyy");
            tokens[$"{prefix}Month"] = moment.ToString("MM");
            tokens[$"{prefix}Day"] = moment.ToString("dd");
            tokens[$"{prefix}Hour"] = moment.ToString("HH");
            tokens[$"{prefix}Minute"] = moment.ToString("mm");
            tokens[$"{prefix}Second"] = moment.ToString("ss");
            tokens[$"{prefix}Date"] = moment.ToString("yyyy-MM-dd");
            tokens[$"{prefix}Time"] = moment.ToString("HH-mm-ss");
        }
    }
}
