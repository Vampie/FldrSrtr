using System;
using System.Text.RegularExpressions;

namespace App.Core.Evaluation
{
    /// <summary>
    /// Wildcard (*, ?) and regex matching, both used by filename conditions and by folder-level
    /// exclusion patterns. Every regex evaluation carries a timeout — untrusted user patterns
    /// must never be able to hang a scan (ReDoS), per the risk noted in the project brief.
    /// </summary>
    public static class PatternMatcher
    {
        private static readonly TimeSpan MatchTimeout = TimeSpan.FromMilliseconds(500);

        public static bool IsWildcardMatch(string text, string pattern, bool caseSensitive)
        {
            string regexPattern = "^" + Regex.Escape(pattern ?? string.Empty)
                .Replace(@"\*", ".*")
                .Replace(@"\?", ".") + "$";

            return IsRegexMatch(text, regexPattern, caseSensitive);
        }

        public static bool IsRegexMatch(string text, string pattern, bool caseSensitive)
        {
            if (string.IsNullOrEmpty(pattern))
            {
                return false;
            }

            RegexOptions options = caseSensitive ? RegexOptions.None : RegexOptions.IgnoreCase;

            try
            {
                return Regex.IsMatch(text ?? string.Empty, pattern, options, MatchTimeout);
            }
            catch (RegexMatchTimeoutException)
            {
                return false;
            }
            catch (ArgumentException)
            {
                // Invalid user-supplied pattern — treat as "does not match" rather than crashing the run.
                return false;
            }
        }
    }
}
