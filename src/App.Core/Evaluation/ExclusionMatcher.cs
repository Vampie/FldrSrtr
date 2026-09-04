using System;
using System.Collections.Generic;
using System.Linq;

namespace App.Core.Evaluation
{
    /// <summary>Folder-level exclusions (§3.11) — independent of any specific rule.</summary>
    public static class ExclusionMatcher
    {
        public static bool IsFileExcluded(string fileName, IEnumerable<string> wildcardPatterns)
        {
            return wildcardPatterns != null &&
                   wildcardPatterns.Any(pattern => PatternMatcher.IsWildcardMatch(fileName, pattern, caseSensitive: false));
        }

        public static bool IsSubfolderExcluded(string folderName, IEnumerable<string> excludedNames)
        {
            return excludedNames != null &&
                   excludedNames.Any(name => string.Equals(folderName, name, StringComparison.OrdinalIgnoreCase));
        }
    }
}
