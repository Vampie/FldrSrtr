using System.Text;
using App.Core.Model;

namespace FldrSrtr
{
    /// <summary>Renders a rule's conditions tree and actions as readable text for the Folders-tab preview panel.</summary>
    public static class RuleSummaryFormatter
    {
        public static string Describe(Rule rule)
        {
            if (rule == null)
            {
                return string.Empty;
            }

            var sb = new StringBuilder();
            sb.AppendLine(Localization.Get("RuleSummary.Conditions"));
            DescribeNode(rule.RootCondition, 1, sb);

            sb.AppendLine();
            sb.AppendLine(Localization.Get("RuleSummary.ActionsInOrder"));
            if (rule.Actions == null || rule.Actions.Count == 0)
            {
                sb.AppendLine("  " + Localization.Get("RuleSummary.NoActions"));
            }
            else
            {
                int index = 1;
                foreach (RuleAction action in rule.Actions)
                {
                    sb.AppendLine($"  {index}. {DescribeAction(action)}");
                    index++;
                }
            }

            return sb.ToString();
        }

        private static void DescribeNode(ConditionNode node, int depth, StringBuilder sb)
        {
            string indent = new string(' ', depth * 2);

            if (node == null)
            {
                sb.AppendLine($"{indent}{Localization.Get("RuleSummary.NoCondition")}");
                return;
            }

            if (node.NodeType == ConditionNodeType.Group)
            {
                if (node.Children == null || node.Children.Count == 0)
                {
                    sb.AppendLine($"{indent}{node.GroupLogic} {Localization.Get("RuleSummary.EmptyGroup")}");
                    return;
                }

                sb.AppendLine($"{indent}{node.GroupLogic}");
                foreach (ConditionNode child in node.Children)
                {
                    DescribeNode(child, depth + 1, sb);
                }
            }
            else if (node.Field == ConditionField.All)
            {
                sb.AppendLine($"{indent}{Localization.Get("RuleSummary.AllFiles")}");
            }
            else
            {
                string caseSensitive = node.CaseSensitive ? " " + Localization.Get("RuleSummary.CaseSensitive") : string.Empty;
                sb.AppendLine($"{indent}{node.Field} {node.Operator} \"{node.Value}\"{caseSensitive}");
            }
        }

        private static string DescribeAction(RuleAction action)
        {
            switch (action.Type)
            {
                case ActionType.DeleteToRecycleBin:
                    return Localization.Get("RuleSummary.Action.DeleteToRecycleBin");
                case ActionType.Open:
                    return Localization.Get("RuleSummary.Action.Open");
                case ActionType.RemoveExtension:
                    return Localization.Get("RuleSummary.Action.RemoveExtension");
                case ActionType.ExecuteExternal:
                    string args = string.IsNullOrEmpty(action.Arguments) ? string.Empty : $" {action.Arguments}";
                    return Localization.Get("RuleSummary.Action.Execute", action.Destination, args);
                case ActionType.AddExtension:
                    return Localization.Get("RuleSummary.Action.AddExtension", action.Destination);
                case ActionType.DeleteTargetIfExists:
                    return Localization.Get("RuleSummary.Action.DeleteTargetIfExists", action.Destination);
                default:
                    return Localization.Get("RuleSummary.Action.Default", action.Type, action.Destination, action.OnConflict);
            }
        }
    }
}
