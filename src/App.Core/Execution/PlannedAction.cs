using App.Core.Model;

namespace App.Core.Execution
{
    public class PlannedAction
    {
        public FileEntry File { get; set; }
        public RuleAction Action { get; set; }
        public string OriginalPath { get; set; }

        /// <summary>
        /// Meaning depends on Action.Type: target file path for Move/Copy/Rename/AddExtension/
        /// RemoveExtension/DeleteTargetIfExists, folder path for CreateFolder, zip path for Zip,
        /// application path for OpenWith/ExecuteExternal. Null for DeleteToRecycleBin/Open.
        /// </summary>
        public string ResolvedDestinationPath { get; set; }

        /// <summary>ExecuteExternal only: resolved command-line arguments.</summary>
        public string ResolvedArguments { get; set; }

        public bool Skipped { get; set; }
        public string SkipReason { get; set; }
    }
}
