using System;
using System.Globalization;
using System.Linq;
using App.Core.Model;

namespace App.Core.Evaluation
{
    /// <summary>Evaluates a rule's nested AND/OR/NOT condition tree against a file.</summary>
    public static class ConditionEvaluator
    {
        public static bool Matches(Rule rule, FileEntry file) => Evaluate(rule.RootCondition, file);

        public static bool Evaluate(ConditionNode node, FileEntry file)
        {
            return node.NodeType == ConditionNodeType.Group
                ? EvaluateGroup(node, file)
                : EvaluateLeaf(node, file);
        }

        private static bool EvaluateGroup(ConditionNode group, FileEntry file)
        {
            if (group.Children == null || group.Children.Count == 0)
            {
                return false;
            }

            switch (group.GroupLogic)
            {
                case GroupLogic.All:
                    return group.Children.All(c => Evaluate(c, file));
                case GroupLogic.Any:
                    return group.Children.Any(c => Evaluate(c, file));
                case GroupLogic.Not:
                    // NOT negates "all children hold" — with one child that's plain negation;
                    // with several it reads as NOT(AND(children)).
                    return !group.Children.All(c => Evaluate(c, file));
                default:
                    return false;
            }
        }

        private static bool EvaluateLeaf(ConditionNode condition, FileEntry file)
        {
            switch (condition.Field)
            {
                case ConditionField.FileName:
                    return EvaluateFileName(condition, file.Name);
                case ConditionField.Extension:
                    return EvaluateExtension(condition, file.Extension);
                case ConditionField.Size:
                    return EvaluateSize(condition, file.SizeBytes);
                case ConditionField.Age:
                    return EvaluateAge(condition, file.ModifiedUtc);
                case ConditionField.Duplicate:
                    return EvaluateDuplicate(condition, file.IsDuplicate);
                default:
                    return false;
            }
        }

        private static bool EvaluateDuplicate(ConditionNode condition, bool isDuplicate)
        {
            bool expected = !string.Equals(condition.Value, "false", StringComparison.OrdinalIgnoreCase);
            return condition.Operator == ConditionOperator.NotEquals ? isDuplicate != expected : isDuplicate == expected;
        }

        private static bool EvaluateFileName(ConditionNode condition, string actual)
        {
            if (condition.Operator == ConditionOperator.Wildcard)
            {
                return PatternMatcher.IsWildcardMatch(actual, condition.Value, condition.CaseSensitive);
            }

            if (condition.Operator == ConditionOperator.Regex)
            {
                return PatternMatcher.IsRegexMatch(actual, condition.Value, condition.CaseSensitive);
            }

            StringComparison comparison = condition.CaseSensitive
                ? StringComparison.Ordinal
                : StringComparison.OrdinalIgnoreCase;

            string expected = condition.Value ?? string.Empty;

            switch (condition.Operator)
            {
                case ConditionOperator.Equals:
                    return string.Equals(actual, expected, comparison);
                case ConditionOperator.NotEquals:
                    return !string.Equals(actual, expected, comparison);
                case ConditionOperator.Contains:
                    return actual.IndexOf(expected, comparison) >= 0;
                case ConditionOperator.StartsWith:
                    return actual.StartsWith(expected, comparison);
                case ConditionOperator.EndsWith:
                    return actual.EndsWith(expected, comparison);
                default:
                    throw new NotSupportedException($"Operator {condition.Operator} wordt niet ondersteund voor filename.");
            }
        }

        private static bool EvaluateExtension(ConditionNode condition, string actualExtension)
        {
            string actual = (actualExtension ?? string.Empty).TrimStart('.');

            switch (condition.Operator)
            {
                case ConditionOperator.Equals:
                    return string.Equals(actual, TrimDot(condition.Value), StringComparison.OrdinalIgnoreCase);
                case ConditionOperator.NotEquals:
                    return !string.Equals(actual, TrimDot(condition.Value), StringComparison.OrdinalIgnoreCase);
                case ConditionOperator.IsOneOf:
                    return SplitList(condition.Value).Any(v => string.Equals(actual, v, StringComparison.OrdinalIgnoreCase));
                case ConditionOperator.IsNotOneOf:
                    return !SplitList(condition.Value).Any(v => string.Equals(actual, v, StringComparison.OrdinalIgnoreCase));
                default:
                    throw new NotSupportedException($"Operator {condition.Operator} wordt niet ondersteund voor extension.");
            }
        }

        private static bool EvaluateSize(ConditionNode condition, long actualBytes)
        {
            long expected = ParseLong(condition.Value);

            switch (condition.Operator)
            {
                case ConditionOperator.Equals: return actualBytes == expected;
                case ConditionOperator.NotEquals: return actualBytes != expected;
                case ConditionOperator.GreaterThan: return actualBytes > expected;
                case ConditionOperator.GreaterOrEqual: return actualBytes >= expected;
                case ConditionOperator.LessThan: return actualBytes < expected;
                case ConditionOperator.LessOrEqual: return actualBytes <= expected;
                default:
                    throw new NotSupportedException($"Operator {condition.Operator} wordt niet ondersteund voor size.");
            }
        }

        private static bool EvaluateAge(ConditionNode condition, DateTime modifiedUtc)
        {
            double ageDays = (DateTime.UtcNow - modifiedUtc).TotalDays;
            double expectedDays = ParseDouble(condition.Value);

            switch (condition.Operator)
            {
                case ConditionOperator.GreaterThan: return ageDays > expectedDays;
                case ConditionOperator.GreaterOrEqual: return ageDays >= expectedDays;
                case ConditionOperator.LessThan: return ageDays < expectedDays;
                case ConditionOperator.LessOrEqual: return ageDays <= expectedDays;
                case ConditionOperator.Equals: return Math.Abs(ageDays - expectedDays) < 1.0;
                default:
                    throw new NotSupportedException($"Operator {condition.Operator} wordt niet ondersteund voor age.");
            }
        }

        private static string TrimDot(string value) => (value ?? string.Empty).TrimStart('.');

        private static string[] SplitList(string value) =>
            (value ?? string.Empty)
                .Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(v => TrimDot(v.Trim()))
                .ToArray();

        private static long ParseLong(string value) =>
            long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out long result) ? result : 0L;

        private static double ParseDouble(string value) =>
            double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double result) ? result : 0.0;
    }
}
