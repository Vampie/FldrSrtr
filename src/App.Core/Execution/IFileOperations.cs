namespace App.Core.Execution
{
    /// <summary>
    /// Everything the rule engine needs to touch disk (or launch a process). Kept as a
    /// Core-owned abstraction so dry-run can share the exact same code path as real execution —
    /// dry-run just never reaches an implementation that mutates anything.
    /// </summary>
    public interface IFileOperations
    {
        bool FileExists(string path);
        void Move(string sourcePath, string destinationPath);
        void Copy(string sourcePath, string destinationPath);
        void DeleteToRecycleBin(string path);

        /// <summary>Hex-encoded SHA-256 of the file's content, for duplicate detection.</summary>
        string ComputeSha256(string path);

        void CreateDirectory(string path);
        void AddToZip(string filePath, string zipPath);
        void OpenFile(string path);
        void OpenFileWith(string applicationPath, string filePath);
        void ExecuteExternal(string executablePath, string arguments);
    }
}
