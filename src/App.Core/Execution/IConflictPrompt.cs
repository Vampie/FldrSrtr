using App.Core.Model;

namespace App.Core.Execution
{
    /// <summary>
    /// Backs ConflictResolution.Ask. UI supplies a real implementation (a modal dialog);
    /// tests supply a fixed-answer fake. Never asks for Ask itself — that would recurse.
    /// </summary>
    public interface IConflictPrompt
    {
        ConflictResolution Resolve(string existingPath, string incomingPath);
    }
}
