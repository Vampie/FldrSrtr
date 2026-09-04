namespace App.Core.Model
{
    public class RuleAction
    {
        public ActionType Type { get; set; }

        /// <summary>
        /// Move/Copy: destination folder. Rename: new file name (no directory). CreateFolder:
        /// folder to create. AddExtension: extension to append. OpenWith/ExecuteExternal: the
        /// application/script path. Zip: path to the archive. Unused for Delete/Open/RemoveExtension.
        /// Supports dynamic variables ({Year}, {FileName}, ...) — see VariableResolver.
        /// </summary>
        public string Destination { get; set; }

        /// <summary>ExecuteExternal only: command-line arguments. Supports the same variables.</summary>
        public string Arguments { get; set; }

        public ConflictResolution OnConflict { get; set; } = ConflictResolution.Rename;
    }
}
