using System;
using System.Globalization;
using System.Linq;
using App.Core.Model;

namespace App.Core.Evaluation
{
    /// <summary>
    /// Evaluates a rule's flat condition list against a file. Nested AND/OR/NOT groups and
    /// regex/wildcard matching are Fase 2 — this only needs to support §3.3's basic operators.
    /// </summary>
    public static class ConditionEvaluator
    {
        public static bool Matches(Rule rule, FileEntry file)
        {
            if (rule.Conditions == null || rule.Conditions.Count == 0)
            {
                return false;
            }

            return rule.Logic == ConditionLogic.Any
                ? rule.Conditions.Any(c => Evaluate(c, file))
                : rule.Conditions.All(c => Evaluate(c, file));
        }

        public static bool Evaluate(Condition condition, FileEntry file)
        {
            switch (condition.Field)
            {
                case ConditionField.FileName:
                    return EvaluateText(condition, file.Name);
                case ConditionField.Extension:
                    return EvaluateExtension(condition, file.Extension);
                case ConditionField.Size:
                    return EvaluateSize(condition, file.SizeBytes);
                case ConditionField.Age:
                    return EvaluateAge(condition, file.ModifiedUtc);
                default:
                    return false;
            }
        }

        private static bool EvaluateText(Condition condition, string actual)
        {
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

        private static bool EvaluateExtension(Condition condition, string actualExtension)
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

        private static bool EvaluateSize(Condition condition, long actualBytes)
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

        private static bool EvaluateAge(Condition condition, DateTime modifiedUtc)
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
