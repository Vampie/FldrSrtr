namespace App.Core.Execution
{
    public class UndoResult
    {
        public bool Success { get; set; }
        public string ErrorMessage { get; set; }

        public static UndoResult Ok() => new UndoResult { Success = true };
        public static UndoResult Fail(string message) => new UndoResult { Success = false, ErrorMessage = message };
    }
}
