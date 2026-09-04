using System;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
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
                    return EvaluateText(condition, file.Name);
                case ConditionField.Extension:
                    return EvaluateExtension(condition, file.Extension);
                case ConditionField.Size:
                    return EvaluateSize(condition, file.SizeBytes);
                case ConditionField.Age:
                    return EvaluateAge(condition, file.ModifiedUtc);
                case ConditionField.Duplicate:
                    return EvaluateDuplicate(condition, file.IsDuplicate);
                case ConditionField.CreatedDate:
                    return EvaluateDate(condition, file.CreatedUtc);
                case ConditionField.ModifiedDate:
                    return EvaluateDate(condition, file.ModifiedUtc);
                case ConditionField.AccessedDate:
                    return EvaluateDate(condition, file.AccessedUtc);
                default:
                    return false;
            }
        }

        private static bool EvaluateDuplicate(ConditionNode condition, bool isDuplicate)
        {
            bool expected = !string.Equals(condition.Value, "false", StringComparison.OrdinalIgnoreCase);
            return condition.Operator == ConditionOperator.NotEquals ? isDuplicate != expected : isDuplicate == expected;
        }

        /// <summary>Shared text matching for FileName and (via EvaluateExtension) Extension.</summary>
        private static bool EvaluateText(ConditionNode condition, string actual)
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
                    return false; // unsupported operator for this field — fail safe rather than crash the run
            }
        }

        private static bool EvaluateExtension(ConditionNode condition, string actualExtension)
        {
            string actual = (actualExtension ?? string.Empty).TrimStart('.');

            switch (condition.Operator)
            {
                case ConditionOperator.IsOneOf:
                    return SplitList(condition.Value).Any(v => string.Equals(actual, v, StringComparison.OrdinalIgnoreCase));
                case ConditionOperator.IsNotOneOf:
                    return !SplitList(condition.Value).Any(v => string.Equals(actual, v, StringComparison.OrdinalIgnoreCase));
                default:
                    // Equals/NotEquals/Contains/StartsWith/EndsWith/Wildcard/Regex — same as FileName,
                    // just compared against the extension text (and values get their leading dot trimmed).
                    return EvaluateText(CloneWithValue(condition, TrimDot(condition.Value)), actual);
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
                    return false; // unsupported operator for this field — fail safe rather than crash the run
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
                    return false; // unsupported operator for this field — fail safe rather than crash the run
            }
        }

        /// <summary>Absolute calendar-date comparison for CreatedDate/ModifiedDate/AccessedDate.</summary>
        private static bool EvaluateDate(ConditionNode condition, DateTime actualUtc)
        {
            DateTime actualLocal = actualUtc.ToLocalTime().Date;

            switch (condition.Operator)
            {
                case ConditionOperator.Before:
                    return TryParseDate(condition.Value, out DateTime before) && actualLocal < before;
                case ConditionOperator.After:
                    return TryParseDate(condition.Value, out DateTime after) && actualLocal > after;
                case ConditionOperator.Between:
                    return TryParseDateRange(condition.Value, out DateTime from, out DateTime to) &&
                           actualLocal >= from && actualLocal <= to;
                case ConditionOperator.Equals:
                    return TryParseDate(condition.Value, out DateTime equalsDate) && actualLocal == equalsDate;
                case ConditionOperator.NotEquals:
                    return TryParseDate(condition.Value, out DateTime notEqualsDate) && actualLocal != notEqualsDate;
                default:
                    return false; // unsupported operator for this field — fail safe rather than crash the run
            }
        }

        private static bool TryParseDate(string value, out DateTime date) =>
            DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out date);

        private static bool TryParseDateRange(string value, out DateTime from, out DateTime to)
        {
            from = default;
            to = default;

            string[] parts = (value ?? string.Empty).Split(',');
            if (parts.Length != 2)
            {
                return false;
            }

            return TryParseDate(parts[0].Trim(), out from) && TryParseDate(parts[1].Trim(), out to);
        }

        private static ConditionNode CloneWithValue(ConditionNode source, string value) => new ConditionNode
        {
            NodeType = source.NodeType,
            Field = source.Field,
            Operator = source.Operator,
            Value = value,
            CaseSensitive = source.CaseSensitive
        };

        private static string TrimDot(string value) => (value ?? string.Empty).TrimStart('.');

        /// <summary>
        /// Splits an IsOneOf/IsNotOneOf list on commas, whitespace, or both — "pdf,docx",
        /// "pdf docx" and "pdf, docx" all work, since users type these inconsistently.
        /// </summary>
        private static string[] SplitList(string value) =>
            Regex.Split((value ?? string.Empty).Trim(), @"[,\s]+")
                .Where(v => !string.IsNullOrEmpty(v))
                .Select(TrimDot)
                .ToArray();

        private static long ParseLong(string value) =>
            long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out long result) ? result : 0L;

        private static double ParseDouble(string value) =>
            double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double result) ? result : 0.0;
    }
}
