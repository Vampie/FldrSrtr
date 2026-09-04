using App.Core.Model;

namespace App.Core.Execution
{
    public class PlannedAction
    {
        public FileEntry File { get; set; }
        public RuleAction Action { get; set; }
        public string OriginalPath { get; set; }

        /// <summary>Final path after conflict resolution. Null for DeleteToRecycleBin.</summary>
        public string ResolvedDestinationPath { get; set; }

        public bool Skipped { get; set; }
        public string SkipReason { get; set; }
    }
}
