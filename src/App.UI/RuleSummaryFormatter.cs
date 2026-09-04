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
            sb.AppendLine("Conditions:");
            DescribeNode(rule.RootCondition, 1, sb);

            sb.AppendLine();
            sb.AppendLine("Actions (in volgorde):");
            if (rule.Actions == null || rule.Actions.Count == 0)
            {
                sb.AppendLine("  (geen acties)");
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
                sb.AppendLine($"{indent}(geen conditie)");
                return;
            }

            if (node.NodeType == ConditionNodeType.Group)
            {
                if (node.Children == null || node.Children.Count == 0)
                {
                    sb.AppendLine($"{indent}{node.GroupLogic} (leeg — matcht nooit)");
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
                sb.AppendLine($"{indent}All (alle bestanden — geen filter)");
            }
            else
            {
                string caseSensitive = node.CaseSensitive ? " [case-sensitive]" : string.Empty;
                sb.AppendLine($"{indent}{node.Field} {node.Operator} \"{node.Value}\"{caseSensitive}");
            }
        }

        private static string DescribeAction(RuleAction action)
        {
            switch (action.Type)
            {
                case ActionType.DeleteToRecycleBin:
                    return "Delete to Recycle Bin";
                case ActionType.Open:
                    return "Open (standaardprogramma)";
                case ActionType.RemoveExtension:
                    return "Remove extension";
                case ActionType.ExecuteExternal:
                    string args = string.IsNullOrEmpty(action.Arguments) ? string.Empty : $" {action.Arguments}";
                    return $"Execute {action.Destination}{args}";
                case ActionType.AddExtension:
                    return $"Add extension \"{action.Destination}\"";
                case ActionType.DeleteTargetIfExists:
                    return $"Delete target if exists: {action.Destination}";
                default:
                    return $"{action.Type} -> {action.Destination} (on conflict: {action.OnConflict})";
            }
        }
    }
}
