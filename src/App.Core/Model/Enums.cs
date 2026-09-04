namespace App.Core.Model
{
    public enum ConditionField
    {
        FileName,
        Extension,
        Size,
        Age,

        /// <summary>True if another file in the same run has identical content (SHA-256).</summary>
        Duplicate
    }

    public enum ConditionOperator
    {
        Equals,
        NotEquals,
        Contains,
        StartsWith,
        EndsWith,
        IsOneOf,
        IsNotOneOf,
        GreaterThan,
        GreaterOrEqual,
        LessThan,
        LessOrEqual,

        /// <summary>FileName only. "*" / "?" globbing.</summary>
        Wildcard,

        /// <summary>FileName only. Evaluated with a match timeout to guard against ReDoS.</summary>
        Regex
    }

    public enum ConditionNodeType
    {
        Leaf,
        Group
    }

    public enum GroupLogic
    {
        All,
        Any,
        Not
    }

    public enum ActionType
    {
        Move,
        Copy,
        Rename,
        DeleteToRecycleBin,

        /// <summary>Opens the file with its default associated application.</summary>
        Open,

        /// <summary>Opens the file with the application at Destination.</summary>
        OpenWith,

        /// <summary>Runs the program/script at Destination, with Arguments (both support variables).</summary>
        ExecuteExternal,

        /// <summary>Ensures the folder at Destination (variables resolved) exists.</summary>
        CreateFolder,

        /// <summary>Appends the extension in Destination, e.g. "bak" -> "file.pdf.bak".</summary>
        AddExtension,

        /// <summary>Strips the current extension, e.g. "file.pdf" -> "file".</summary>
        RemoveExtension,

        /// <summary>Adds the file to the zip archive at Destination (created if missing).</summary>
        Zip
    }

    public enum ConflictResolution
    {
        Skip,
        Overwrite,
        Rename,

        /// <summary>Delegates to IConflictPrompt at plan time; falls back to Rename if none is supplied.</summary>
        Ask
    }
}
