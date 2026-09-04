using System;
using System.IO;
using App.Core.Model;

namespace App.Core.Execution
{
    /// <summary>
    /// Resolves {FileName}, {Year}, ... tokens in an action's Destination/Arguments (§3.6).
    /// {FileName}/{Extension} reflect the file's *current* location in the action chain (so a
    /// rename earlier in the same rule is visible to a later action); {OriginalName} /
    /// {OriginalExtension} / {FileSize} reflect the file as it was when the folder was scanned.
    /// {Year}/{Month}/... use the moment the rule runs, not the file's own timestamps.
    /// </summary>
    public static class VariableResolver
    {
        public static string Resolve(string template, FileEntry originalFile, string currentPath, DateTime now)
        {
            if (string.IsNullOrEmpty(template))
            {
                return template;
            }

            string currentName = Path.GetFileNameWithoutExtension(currentPath);
            string currentExtension = Path.GetExtension(currentPath).TrimStart('.');
            string originalName = Path.GetFileNameWithoutExtension(originalFile.Name);

            return template
                .Replace("{FileName}", currentName)
                .Replace("{OriginalName}", originalName)
                .Replace("{Extension}", currentExtension)
                .Replace("{OriginalExtension}", originalFile.Extension)
                .Replace("{FullPath}", currentPath)
                .Replace("{Directory}", Path.GetDirectoryName(currentPath) ?? string.Empty)
                .Replace("{FileSize}", originalFile.SizeBytes.ToString())
                .Replace("{Year}", now.ToString("yyyy"))
                .Replace("{Month}", now.ToString("MM"))
                .Replace("{Day}", now.ToString("dd"))
                .Replace("{Hour}", now.ToString("HH"))
                .Replace("{Minute}", now.ToString("mm"))
                .Replace("{Second}", now.ToString("ss"))
                .Replace("{Date}", now.ToString("yyyy-MM-dd"))
                .Replace("{Time}", now.ToString("HH-mm-ss"));
        }
    }
}
