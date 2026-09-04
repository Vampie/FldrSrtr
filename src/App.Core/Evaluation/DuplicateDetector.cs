using System.Collections.Generic;
using System.Linq;
using App.Core.Execution;
using App.Core.Model;

namespace App.Core.Evaluation
{
    /// <summary>
    /// Flags every file that has an identical-content twin elsewhere in the same file list,
    /// except the first one encountered (that one stays the "original" / not a duplicate).
    /// Groups by size first per §4.1 — SHA-256 is only computed within a same-size group.
    /// </summary>
    public static class DuplicateDetector
    {
        public static void MarkDuplicates(IReadOnlyList<FileEntry> files, IFileOperations fileOps)
        {
            foreach (FileEntry file in files)
            {
                file.IsDuplicate = false;
            }

            var bySize = files.GroupBy(f => f.SizeBytes).Where(g => g.Count() > 1);

            foreach (var sizeGroup in bySize)
            {
                var byHash = sizeGroup
                    .Select(f => (File: f, Hash: fileOps.ComputeSha256(f.FullPath)))
                    .GroupBy(x => x.Hash);

                foreach (var hashGroup in byHash.Where(g => g.Count() > 1))
                {
                    foreach (var entry in hashGroup.Skip(1))
                    {
                        entry.File.IsDuplicate = true;
                    }
                }
            }
        }

        public static bool RuleChecksDuplicates(ConditionNode node)
        {
            if (node == null)
            {
                return false;
            }

            if (node.NodeType == ConditionNodeType.Leaf)
            {
                return node.Field == ConditionField.Duplicate;
            }

            return node.Children.Any(RuleChecksDuplicates);
        }
    }
}
