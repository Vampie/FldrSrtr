using System;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using App.Core.Model;
using Condition = App.Core.Model.Condition;

namespace FoldrSortr
{
    public partial class RuleEditorWindow : Window
    {
        private readonly Rule _rule;
        private readonly ObservableCollection<Condition> _conditions;
        private readonly ObservableCollection<RuleAction> _actions;

        public RuleEditorWindow(Rule rule)
        {
            InitializeComponent();
            _rule = rule;

            LogicComboBox.ItemsSource = Enum.GetValues(typeof(ConditionLogic));
            FieldColumn.ItemsSource = Enum.GetValues(typeof(ConditionField));
            OperatorColumn.ItemsSource = Enum.GetValues(typeof(ConditionOperator));
            ActionTypeColumn.ItemsSource = Enum.GetValues(typeof(ActionType));
            OnConflictColumn.ItemsSource = Enum.GetValues(typeof(ConflictResolution));

            NameTextBox.Text = rule.Name;
            EnabledCheckBox.IsChecked = rule.Enabled;
            LogicComboBox.SelectedItem = rule.Logic;

            _conditions = new ObservableCollection<Condition>(rule.Conditions);
            _actions = new ObservableCollection<RuleAction>(rule.Actions);
            ConditionsGrid.ItemsSource = _conditions;
            ActionsGrid.ItemsSource = _actions;
        }

        private void AddCondition_Click(object sender, RoutedEventArgs e)
        {
            _conditions.Add(new Condition { Field = ConditionField.Extension, Operator = ConditionOperator.Equals });
        }

        private void RemoveCondition_Click(object sender, RoutedEventArgs e)
        {
            if (((Button)sender).DataContext is Condition condition)
            {
                _conditions.Remove(condition);
            }
        }

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

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(NameTextBox.Text))
            {
                MessageBox.Show(this, "Geef de regel een naam.", "FoldrSortr", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            _rule.Name = NameTextBox.Text.Trim();
            _rule.Enabled = EnabledCheckBox.IsChecked == true;
            _rule.Logic = (ConditionLogic)LogicComboBox.SelectedItem;

            _rule.Conditions.Clear();
            foreach (Condition condition in _conditions)
            {
                _rule.Conditions.Add(condition);
            }

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
