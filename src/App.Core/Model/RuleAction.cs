namespace App.Core.Model
{
    public class RuleAction
    {
        public ActionType Type { get; set; }

        /// <summary>
        /// Move/Copy: destination folder. Rename: new file name (no directory). Unused for delete.
        /// Literal only in Fase 1 — dynamic variables like {Year} arrive in Fase 3.
        /// </summary>
        public string Destination { get; set; }

        public ConflictResolution OnConflict { get; set; } = ConflictResolution.Rename;
    }
}
