using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using App.Core.Model;

namespace App.Core.Evaluation
{
    /// <summary>
    /// Checks a rule for problems the UI should surface before saving — a mismatched
    /// field/operator, a missing value, an invalid regex, an action with no destination, and so
    /// on. ConditionEvaluator/RuleEngine still fail safe on all of these (never crash), but a
    /// silently-broken rule that never matches anything is still a bad experience worth flagging.
    /// </summary>
    public static class RuleValidator
    {
        public static List<string> Validate(Rule rule)
        {
            var issues = new List<string>();

            if (string.IsNullOrWhiteSpace(rule.Name))
            {
                issues.Add("De regel heeft geen naam.");
            }

            ValidateNode(rule.RootCondition, issues);

            if (rule.Actions == null || rule.Actions.Count == 0)
            {
                issues.Add("De regel heeft geen acties.");
            }
            else
            {
                foreach (RuleAction action in rule.Actions)
                {
                    ValidateAction(action, issues);
                }
            }

            return issues;
        }

        private static void ValidateNode(ConditionNode node, List<string> issues)
        {
            if (node == null)
            {
                issues.Add("Er ontbreekt een conditie.");
                return;
            }

            if (node.NodeType == ConditionNodeType.Group)
            {
                if (node.Children == null || node.Children.Count == 0)
                {
                    issues.Add("Een lege groep matcht nooit een bestand.");
                    return;
                }

                foreach (ConditionNode child in node.Children)
                {
                    ValidateNode(child, issues);
                }
            }
            else
            {
                ValidateLeaf(node, issues);
            }
        }

        private static void ValidateLeaf(ConditionNode node, List<string> issues)
        {
            if (!ConditionOperatorCatalog.SupportedOperators(node.Field).Contains(node.Operator))
            {
                issues.Add($"Operator '{node.Operator}' is niet geldig voor veld '{node.Field}'.");
                return;
            }

            bool needsValue = node.Field != ConditionField.Duplicate && node.Field != ConditionField.All;
            if (needsValue && string.IsNullOrWhiteSpace(node.Value))
            {
                issues.Add($"Conditie op '{node.Field}' heeft geen waarde ingevuld.");
                return;
            }

            if (node.Operator == ConditionOperator.Regex)
            {
                try
                {
                    _ = new Regex(node.Value);
                }
                catch (ArgumentException)
                {
                    issues.Add($"Ongeldige reguliere expressie bij '{node.Field}': '{node.Value}'.");
                }
            }

            if (IsDateField(node.Field))
            {
                ValidateDateValue(node, issues);
            }

            if (node.Field == ConditionField.Size || node.Field == ConditionField.Age)
            {
                ValidateNumericValue(node, issues);
            }
        }

        private static bool IsDateField(ConditionField field) =>
            field == ConditionField.CreatedDate || field == ConditionField.ModifiedDate || field == ConditionField.AccessedDate;

        private static void ValidateDateValue(ConditionNode node, List<string> issues)
        {
            if (node.Operator == ConditionOperator.Between)
            {
                string[] parts = (node.Value ?? string.Empty).Split(',');
                if (parts.Length != 2 || !DateTime.TryParse(parts[0].Trim(), out _) || !DateTime.TryParse(parts[1].Trim(), out _))
                {
                    issues.Add($"'{node.Field}' Between verwacht twee data gescheiden door een komma, bv. 2026-01-01,2026-03-01 (huidige waarde: '{node.Value}').");
                }
            }
            else if (!DateTime.TryParse(node.Value, out _))
            {
                issues.Add($"'{node.Value}' is geen geldige datum voor '{node.Field}'.");
            }
        }

        private static void ValidateNumericValue(ConditionNode node, List<string> issues)
        {
            if (!double.TryParse(node.Value, out _))
            {
                issues.Add($"'{node.Value}' is geen geldig getal voor '{node.Field}'.");
            }
        }

        private static void ValidateAction(RuleAction action, List<string> issues)
        {
            bool needsDestination = action.Type == ActionType.Move || action.Type == ActionType.Copy ||
                                     action.Type == ActionType.Rename || action.Type == ActionType.AddExtension ||
                                     action.Type == ActionType.CreateFolder || action.Type == ActionType.Zip ||
                                     action.Type == ActionType.OpenWith || action.Type == ActionType.ExecuteExternal;

            if (needsDestination && string.IsNullOrWhiteSpace(action.Destination))
            {
                issues.Add($"Actie '{action.Type}' heeft geen Destination ingevuld.");
            }
        }
    }
}
