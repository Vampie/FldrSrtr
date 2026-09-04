using App.Core.Model;

namespace App.Core.Evaluation
{
    /// <summary>
    /// Single source of truth for which operators are valid per field — mirrors the switch
    /// statements in ConditionEvaluator. The UI uses this to keep the Operator dropdown limited
    /// to valid choices; ConditionEvaluator still fails safe (returns false, never throws) on a
    /// mismatched combination that reaches it anyway (e.g. from a hand-edited or imported rule).
    /// </summary>
    public static class ConditionOperatorCatalog
    {
        public static ConditionOperator[] SupportedOperators(ConditionField field)
        {
            switch (field)
            {
                case ConditionField.FileName:
                    return new[]
                    {
                        ConditionOperator.Equals, ConditionOperator.NotEquals,
                        ConditionOperator.Contains, ConditionOperator.StartsWith, ConditionOperator.EndsWith,
                        ConditionOperator.Wildcard, ConditionOperator.Regex
                    };
                case ConditionField.Extension:
                    return new[]
                    {
                        ConditionOperator.Equals, ConditionOperator.NotEquals,
                        ConditionOperator.IsOneOf, ConditionOperator.IsNotOneOf
                    };
                case ConditionField.Size:
                    return new[]
                    {
                        ConditionOperator.Equals, ConditionOperator.NotEquals,
                        ConditionOperator.GreaterThan, ConditionOperator.GreaterOrEqual,
                        ConditionOperator.LessThan, ConditionOperator.LessOrEqual
                    };
                case ConditionField.Age:
                    return new[]
                    {
                        ConditionOperator.Equals,
                        ConditionOperator.GreaterThan, ConditionOperator.GreaterOrEqual,
                        ConditionOperator.LessThan, ConditionOperator.LessOrEqual
                    };
                case ConditionField.Duplicate:
                    return new[] { ConditionOperator.Equals, ConditionOperator.NotEquals };
                default:
                    return new ConditionOperator[0];
            }
        }
    }
}
