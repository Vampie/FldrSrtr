namespace App.Core.Model
{
    public enum ConditionField
    {
        FileName,
        Extension,
        Size,
        Age
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
        DeleteToRecycleBin
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
