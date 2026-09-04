using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using App.Core.Model;

namespace App.Core.Execution
{
    /// <summary>
    /// Resolves {FileName}, {Year}, {CreatedYear}, ... tokens in an action's Destination/Arguments
    /// (§3.6). Tokens are matched case-insensitively — {filename} and {FILENAME} both work.
    /// {FileName}/{Extension} reflect the file's *current* location in the action chain (so a
    /// rename earlier in the same rule is visible to a later action); {OriginalName} /
    /// {OriginalExtension} / {FileSize} reflect the file as it was when the folder was scanned.
    /// {Year}/{Month}/... use the moment the rule runs; {CreatedYear}/{ModifiedYear}/... use the
    /// file's own Created/Modified timestamps instead.
    /// </summary>
    public static class VariableResolver
    {
        private static readonly Regex TokenPattern = new Regex(@"\{(\w+)\}", RegexOptions.Compiled);

        public static string Resolve(string template, FileEntry originalFile, string currentPath, DateTime now)
        {
            if (string.IsNullOrEmpty(template))
            {
                return template;
            }

            var tokens = BuildTokenMap(originalFile, currentPath, now);

            return TokenPattern.Replace(template, match =>
            {
                string key = match.Groups[1].Value;
                return tokens.TryGetValue(key, out string value) ? value : match.Value;
            });
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
