namespace App.Core.Execution
{
    /// <summary>
    /// Everything the rule engine needs to touch disk. Kept as a Core-owned abstraction (rather
    /// than depending on a specific IO library) so dry-run can share the exact same code path as
    /// real execution — dry-run just never reaches an implementation that mutates anything.
    /// </summary>
    public interface IFileOperations
    {
        bool FileExists(string path);
        void Move(string sourcePath, string destinationPath);
        void Copy(string sourcePath, string destinationPath);
        void DeleteToRecycleBin(string path);
    }
}
