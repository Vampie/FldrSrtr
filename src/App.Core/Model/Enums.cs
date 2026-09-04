namespace App.Core.Model
{
    public enum ConditionField
    {
        /// <summary>Always matches — no filter applied. Lets a rule act on every file in scope.</summary>
        All,

        FileName,
        Extension,
        Size,

        /// <summary>Relative — "older/newer than X days", based on ModifiedUtc.</summary>
        Age,

        /// <summary>True if another file in the same run has identical content (SHA-256).</summary>
        Duplicate,

        /// <summary>Absolute calendar date — Before/After/Between/Equals. Based on CreatedUtc.</summary>
        CreatedDate,

        /// <summary>Absolute calendar date — Before/After/Between/Equals. Based on ModifiedUtc.</summary>
        ModifiedDate,

        /// <summary>Absolute calendar date — Before/After/Between/Equals. Based on AccessedUtc.</summary>
        AccessedDate
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

        /// <summary>FileName/Extension only. "*" / "?" globbing.</summary>
        Wildcard,

        /// <summary>FileName/Extension only. Evaluated with a match timeout to guard against ReDoS.</summary>
        Regex,

        /// <summary>Date fields only. Value is a single date (e.g. "2026-03-01").</summary>
        Before,

        /// <summary>Date fields only. Value is a single date (e.g. "2026-03-01").</summary>
        After,

        /// <summary>Date fields only. Value is "from,to" (e.g. "2026-01-01,2026-03-01").</summary>
        Between
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
