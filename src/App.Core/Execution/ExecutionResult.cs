namespace App.Core.Execution
{
    public class ExecutionResult
    {
        public PlannedAction Plan { get; set; }
        public bool Success { get; set; }
        public string ErrorMessage { get; set; }
    }
}
