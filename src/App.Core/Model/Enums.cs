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
        LessOrEqual
    }

    public enum ConditionLogic
    {
        All,
        Any
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
        Rename
    }
}
