using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using App.Core.Model;

namespace FldrSrtr
{
    public partial class RuleEditorWindow : Window
    {
        private static readonly ConditionField[] BasicFields = { ConditionField.FileName, ConditionField.Extension, ConditionField.Size, ConditionField.Age };
        private static readonly ConditionField[] AllFields = (ConditionField[])Enum.GetValues(typeof(ConditionField));

        private static readonly ConditionOperator[] BasicOperators = ((ConditionOperator[])Enum.GetValues(typeof(ConditionOperator)))
            .Where(op => op != ConditionOperator.Wildcard && op != ConditionOperator.Regex).ToArray();
        private static readonly ConditionOperator[] AllOperators = (ConditionOperator[])Enum.GetValues(typeof(ConditionOperator));

        private static readonly ActionType[] BasicActions = { ActionType.Move, ActionType.Copy, ActionType.Rename, ActionType.DeleteToRecycleBin };
        private static readonly ActionType[] AllActions = (ActionType[])Enum.GetValues(typeof(ActionType));

        private readonly Rule _rule;
        private readonly ObservableCollection<RuleAction> _actions;
        private ConditionNode _selectedNode;
        private bool _suppressEvents;

        public RuleEditorWindow(Rule rule)
        {
            InitializeComponent();
            _rule = rule;

            GroupLogicComboBox.ItemsSource = Enum.GetValues(typeof(GroupLogic));
            OnConflictColumn.ItemsSource = Enum.GetValues(typeof(ConflictResolution));

            NameTextBox.Text = rule.Name;
            EnabledCheckBox.IsChecked = rule.Enabled;

            if (rule.RootCondition == null || rule.RootCondition.NodeType != ConditionNodeType.Group)
            {
                rule.RootCondition = ConditionNode.NewGroup();
            }
            ConditionsTree.ItemsSource = new[] { rule.RootCondition };

            _actions = new ObservableCollection<RuleAction>(rule.Actions);
            ActionsGrid.ItemsSource = _actions;

            AdvancedModeCheckBox.IsChecked = RuleUsesAdvancedFeatures(rule);
            ApplyAdvancedMode();
        }

        private void AdvancedModeCheckBox_Changed(object sender, RoutedEventArgs e) => ApplyAdvancedMode();

        private void ApplyAdvancedMode()
        {
            bool advanced = AdvancedModeCheckBox.IsChecked == true;
            VariableHelpPanel.Visibility = advanced ? Visibility.Visible : Visibility.Collapsed;
            FieldComboBox.ItemsSource = advanced ? AllFields : BasicFields;
            OperatorComboBox.ItemsSource = advanced ? AllOperators : BasicOperators;
            ActionTypeColumn.ItemsSource = advanced ? AllActions : BasicActions;
        }

        private static bool RuleUsesAdvancedFeatures(Rule rule)
        {
            if (rule.Actions.Any(a => !BasicActions.Contains(a.Type)))
            {
                return true;
            }
            return UsesAdvancedCondition(rule.RootCondition);
        }

        private static bool UsesAdvancedCondition(ConditionNode node)
        {
            if (node == null)
            {
                return false;
            }
            if (node.NodeType == ConditionNodeType.Leaf)
            {
                return !BasicFields.Contains(node.Field) || !BasicOperators.Contains(node.Operator);
            }
            return node.Children.Any(UsesAdvancedCondition);
        }

        // ----- Conditions tree -----

        private void ConditionsTree_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            _selectedNode = e.NewValue as ConditionNode;
            _suppressEvents = true;
            try
            {
                bool isRoot = _selectedNode == _rule.RootCondition;

                if (_selectedNode == null)
                {
                    NoSelectionText.Visibility = Visibility.Visible;
                    GroupDetailsPanel.Visibility = Visibility.Collapsed;
                    LeafDetailsPanel.Visibility = Visibility.Collapsed;
                }
                else if (_selectedNode.NodeType == ConditionNodeType.Group)
                {
                    NoSelectionText.Visibility = Visibility.Collapsed;
                    GroupDetailsPanel.Visibility = Visibility.Visible;
                    LeafDetailsPanel.Visibility = Visibility.Collapsed;
                    GroupLogicComboBox.SelectedItem = _selectedNode.GroupLogic;
                    RemoveGroupButton.IsEnabled = !isRoot;
                }
                else
                {
                    NoSelectionText.Visibility = Visibility.Collapsed;
                    GroupDetailsPanel.Visibility = Visibility.Collapsed;
                    LeafDetailsPanel.Visibility = Visibility.Visible;
                    FieldComboBox.SelectedItem = _selectedNode.Field;
                    OperatorComboBox.SelectedItem = _selectedNode.Operator;
                    ValueTextBox.Text = _selectedNode.Value;
                    CaseSensitiveCheckBox.IsChecked = _selectedNode.CaseSensitive;
                    DuplicateHintText.Visibility = _selectedNode.Field == ConditionField.Duplicate ? Visibility.Visible : Visibility.Collapsed;
                }
            }
            finally
            {
                _suppressEvents = false;
            }
        }

        private void GroupLogicComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_suppressEvents || _selectedNode == null || GroupLogicComboBox.SelectedItem == null)
            {
                return;
            }
            _selectedNode.GroupLogic = (GroupLogic)GroupLogicComboBox.SelectedItem;
        }

        private void LeafField_Changed(object sender, RoutedEventArgs e)
        {
            if (_suppressEvents || _selectedNode == null || _selectedNode.NodeType != ConditionNodeType.Leaf)
            {
                return;
            }

            if (FieldComboBox.SelectedItem != null)
            {
                _selectedNode.Field = (ConditionField)FieldComboBox.SelectedItem;
                DuplicateHintText.Visibility = _selectedNode.Field == ConditionField.Duplicate ? Visibility.Visible : Visibility.Collapsed;
            }
            if (OperatorComboBox.SelectedItem != null)
            {
                _selectedNode.Operator = (ConditionOperator)OperatorComboBox.SelectedItem;
            }
            _selectedNode.Value = ValueTextBox.Text;
            _selectedNode.CaseSensitive = CaseSensitiveCheckBox.IsChecked == true;
        }

        private void AddConditionLeaf_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedNode?.NodeType != ConditionNodeType.Group)
            {
                return;
            }
            _selectedNode.Children.Add(ConditionNode.NewLeaf(ConditionField.Extension, ConditionOperator.Equals, string.Empty));
        }

        private void AddConditionGroup_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedNode?.NodeType != ConditionNodeType.Group)
            {
                return;
            }
            _selectedNode.Children.Add(ConditionNode.NewGroup());
        }

        private void RemoveSelectedNode_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedNode == null || _selectedNode == _rule.RootCondition)
            {
                return;
            }

            ConditionNode parent = FindParent(_rule.RootCondition, _selectedNode);
            parent?.Children.Remove(_selectedNode);
        }

        private static ConditionNode FindParent(ConditionNode current, ConditionNode target)
        {
            if (current.NodeType != ConditionNodeType.Group)
            {
                return null;
            }

            if (current.Children.Contains(target))
            {
                return current;
            }

            foreach (ConditionNode child in current.Children)
            {
                ConditionNode found = FindParent(child, target);
                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }

        // ----- Actions -----

        private void AddAction_Click(object sender, RoutedEventArgs e)
        {
            _actions.Add(new RuleAction { Type = ActionType.Move });
        }

        private void RemoveAction_Click(object sender, RoutedEventArgs e)
        {
            if (((Button)sender).DataContext is RuleAction action)
            {
                _actions.Remove(action);
            }
        }

        private void MoveActionUp_Click(object sender, RoutedEventArgs e)
        {
            if (((Button)sender).DataContext is RuleAction action)
            {
                int index = _actions.IndexOf(action);
                if (index > 0)
                {
                    _actions.Move(index, index - 1);
                }
            }
        }

        private void MoveActionDown_Click(object sender, RoutedEventArgs e)
        {
            if (((Button)sender).DataContext is RuleAction action)
            {
                int index = _actions.IndexOf(action);
                if (index >= 0 && index < _actions.Count - 1)
                {
                    _actions.Move(index, index + 1);
                }
            }
        }

        // ----- Save / Cancel -----

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(NameTextBox.Text))
            {
                MessageBox.Show(this, "Geef de regel een naam.", "FldrSrtr", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            _rule.Name = NameTextBox.Text.Trim();
            _rule.Enabled = EnabledCheckBox.IsChecked == true;

            _rule.Actions.Clear();
            foreach (RuleAction action in _actions)
            {
                _rule.Actions.Add(action);
            }

            DialogResult = true;
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}
