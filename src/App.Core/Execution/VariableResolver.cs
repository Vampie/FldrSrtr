using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using App.Core.Model;

namespace App.Core.Execution
{
    /// <summary>
    /// Resolves {FileName}, {Year}, {CreatedYear}, {Counter}, {Guid}, {Random}, ... tokens in an
    /// action's Destination/Arguments (§3.6). Tokens are matched case-insensitively — {filename}
    /// and {FILENAME} both work.
    /// {FileName}/{Extension} reflect the file's *current* location in the action chain (so a
    /// rename earlier in the same rule is visible to a later action); {OriginalName} /
    /// {OriginalExtension} / {FileSize} reflect the file as it was when the folder was scanned.
    /// {Year}/{Month}/... use the moment the rule runs; {CreatedYear}/{ModifiedYear}/... use the
    /// file's own Created/Modified timestamps instead.
    /// {Counter}/{Counter:start}/{Counter:start:step} is resolved via the counterResolver
    /// callback — RuleEngine supplies one that's fresh per run (never persisted) and consistent
    /// across every action applied to the same file.
    /// {Guid}, {UnixTimestamp}, {UnixTimestampMicro}, {Random}/{Random:0000}, and
    /// {RandomString}/{RandomString:12} are generated fresh on every single resolve — unlike
    /// {Counter}, nothing keeps them consistent across chained actions for the same file.
    /// </summary>
    public static class VariableResolver
    {
        private static readonly Regex TokenPattern = new Regex(@"\{([^{}]+)\}", RegexOptions.Compiled);
        private static readonly DateTime UnixEpoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        private static readonly Random RandomGenerator = new Random();
        private const string RandomStringAlphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";

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

                if (IsSpec(spec, "Counter"))
                {
                    return counterResolver != null ? counterResolver(spec).ToString(CultureInfo.InvariantCulture) : match.Value;
                }
                if (spec.Equals("Guid", StringComparison.OrdinalIgnoreCase))
                {
                    return Guid.NewGuid().ToString("N");
                }
                if (spec.Equals("UnixTimestamp", StringComparison.OrdinalIgnoreCase))
                {
                    return ((long)(DateTime.UtcNow - UnixEpoch).TotalSeconds).ToString(CultureInfo.InvariantCulture);
                }
                if (spec.Equals("UnixTimestampMicro", StringComparison.OrdinalIgnoreCase))
                {
                    return ((DateTime.UtcNow.Ticks - UnixEpoch.Ticks) / 10).ToString(CultureInfo.InvariantCulture);
                }
                if (IsSpec(spec, "RandomString"))
                {
                    return ResolveRandomString(spec);
                }
                if (IsSpec(spec, "Random"))
                {
                    return ResolveRandomNumber(spec);
                }

                return tokens.TryGetValue(spec, out string value) ? value : match.Value;
            });
        }

        private static bool IsSpec(string spec, string name) =>
            spec.Equals(name, StringComparison.OrdinalIgnoreCase) ||
            spec.StartsWith(name + ":", StringComparison.OrdinalIgnoreCase);

        private static string ExtractParameter(string spec)
        {
            int colonIndex = spec.IndexOf(':');
            return colonIndex >= 0 && colonIndex < spec.Length - 1 ? spec.Substring(colonIndex + 1) : null;
        }

        /// <summary>{Random} defaults to a 6-digit zero-padded number; {Random:0000} uses the
        /// parameter as a .NET custom numeric format string (both its length — the digit range —
        /// and its exact padding style, e.g. "###0" vs "0000").</summary>
        private static string ResolveRandomNumber(string spec)
        {
            string pattern = ExtractParameter(spec);
            if (string.IsNullOrEmpty(pattern) || !pattern.All(c => c == '0' || c == '#'))
            {
                pattern = "000000";
            }

            int digitCount = Math.Min(pattern.Length, 18);
            long max = (long)Math.Pow(10, digitCount);
            long value = (long)(RandomGenerator.NextDouble() * max);
            return value.ToString(pattern, CultureInfo.InvariantCulture);
        }

        /// <summary>{RandomString} defaults to 8 characters; {RandomString:12} sets the length.</summary>
        private static string ResolveRandomString(string spec)
        {
            string parameter = ExtractParameter(spec);
            int length = 8;
            if (!string.IsNullOrEmpty(parameter) &&
                int.TryParse(parameter, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsedLength) &&
                parsedLength > 0)
            {
                length = Math.Min(parsedLength, 256);
            }

            var chars = new char[length];
            for (int i = 0; i < length; i++)
            {
                chars[i] = RandomStringAlphabet[RandomGenerator.Next(RandomStringAlphabet.Length)];
            }
            return new string(chars);
        }

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
