namespace App.Core.Model
{
    /// <summary>
    /// Minimal description of a completed action needed to reverse it. Deliberately doesn't
    /// carry timestamps/ids/rule name — those live in the activity log (Infrastructure); Core
    /// only needs enough to know what to move back where.
    /// </summary>
    public class UndoableAction
    {
        public ActionType ActionType { get; set; }
        public string OriginalPath { get; set; }
        public string NewPath { get; set; }
    }
}
