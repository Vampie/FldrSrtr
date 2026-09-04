using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using App.Core.Evaluation;
using App.Core.Model;

namespace FldrSrtr
{
    public partial class RuleEditorWindow : Window
    {
        private static readonly ConditionField[] BasicFields = { ConditionField.All, ConditionField.FileName, ConditionField.Extension, ConditionField.Size, ConditionField.Age };
        private static readonly ConditionField[] AllFields = (ConditionField[])Enum.GetValues(typeof(ConditionField));

        private static readonly ConditionOperator[] BasicOperators = ((ConditionOperator[])Enum.GetValues(typeof(ConditionOperator)))
            .Where(op => op != ConditionOperator.Wildcard && op != ConditionOperator.Regex &&
                         op != ConditionOperator.Before && op != ConditionOperator.After && op != ConditionOperator.Between)
            .ToArray();
        private static readonly ConditionOperator[] AllOperators = (ConditionOperator[])Enum.GetValues(typeof(ConditionOperator));

        private static readonly ActionType[] BasicActions = { ActionType.Move, ActionType.Copy, ActionType.Rename, ActionType.DeleteToRecycleBin };
        private static readonly ActionType[] AllActions = (ActionType[])Enum.GetValues(typeof(ActionType));

        /// <summary>
        /// Insert Variable menu, grouped into submenus so the list stays manageable. A null entry
        /// renders as a separator (used in "File" to split general/Created/Modified properties).
        /// </summary>
        private static readonly (string Group, string[] Tokens)[] VariableGroups =
        {
            ("Algemeen", new[] { "{Counter:1:1}", "{Guid}", "{Random:0000}", "{RandomString:8}" }),
            ("File", new[]
            {
                "{FileName}", "{OriginalName}", "{Extension}", "{OriginalExtension}", "{FullPath}", "{Directory}", "{FileSize}",
                null,
                "{CreatedYear}", "{CreatedMonth}", "{CreatedDay}", "{CreatedHour}", "{CreatedMinute}", "{CreatedSecond}", "{CreatedDate}", "{CreatedTime}",
                null,
                "{ModifiedYear}", "{ModifiedMonth}", "{ModifiedDay}", "{ModifiedHour}", "{ModifiedMinute}", "{ModifiedSecond}", "{ModifiedDate}", "{ModifiedTime}"
            }),
            ("Datum (huidige datum)", new[] { "{Year}", "{Month}", "{Day}", "{Hour}", "{Minute}", "{Second}", "{Date}", "{Time}", "{UnixTimestamp}", "{UnixTimestampMicro}" })
        };

        private static readonly Dictionary<ActionType, string> ActionHelp = new Dictionary<ActionType, string>
        {
            [ActionType.Move] = "Verplaatst het bestand naar Destination. Een map die nog niet bestaat wordt aangemaakt.",
            [ActionType.Copy] = "Kopieert het bestand naar Destination. Het origineel blijft staan.",
            [ActionType.Rename] = "Hernoemt het bestand. Destination is de nieuwe bestandsnaam (geen pad), bv. {FileName}_archief.{Extension}.",
            [ActionType.DeleteToRecycleBin] = "Verplaatst het bestand naar de Prullenbak. Geen Destination nodig.",
            [ActionType.Open] = "Opent het bestand met het standaardprogramma van Windows. Geen Destination nodig.",
            [ActionType.OpenWith] = "Opent het bestand met het programma op het pad in Destination (bv. C:\\Apps\\Reader.exe).",
            [ActionType.ExecuteExternal] = "Start het programma/script in Destination. Arguments zijn de commandoregel-parameters die worden meegegeven, bv. \"{FullPath}\" om het bestandspad door te geven.",
            [ActionType.CreateFolder] = "Maakt de map in Destination aan als die nog niet bestaat (bv. om alvast een archiefmap klaar te zetten).",
            [ActionType.AddExtension] = "Voegt een extensie toe aan de bestandsnaam. Destination is enkel de extensie (zonder punt), bv. 'bak' maakt van 'file.pdf' -> 'file.pdf.bak'.",
            [ActionType.RemoveExtension] = "Verwijdert de huidige extensie, bv. 'file.pdf' -> 'file'. Geen Destination nodig.",
            [ActionType.Zip] = "Voegt het bestand toe aan het zip-archief in Destination (wordt aangemaakt als het nog niet bestaat).",
            [ActionType.DeleteTargetIfExists] = "Verplaatst het bestand op Destination naar de Prullenbak, maar alleen als het bestaat — anders gebeurt er niets. Raakt niet aan het bestand dat deze regel verwerkt. Handig vlak vóór een Move/Copy naar diezelfde Destination."
        };

        private readonly Rule _rule;
        private readonly ObservableCollection<RuleAction> _actions;
        private ConditionNode _selectedNode;
        private RuleAction _selectedAction;
        private bool _suppressEvents;

        public RuleEditorWindow(Rule rule)
        {
            InitializeComponent();
            _rule = rule;

            GroupLogicComboBox.ItemsSource = Enum.GetValues(typeof(GroupLogic));
            OnConflictComboBox.ItemsSource = Enum.GetValues(typeof(ConflictResolution));

            NameTextBox.Text = rule.Name;
            EnabledCheckBox.IsChecked = rule.Enabled;

            if (rule.RootCondition == null || rule.RootCondition.NodeType != ConditionNodeType.Group)
            {
                rule.RootCondition = ConditionNode.NewGroup();
            }
            ConditionsTree.ItemsSource = new[] { rule.RootCondition };

            _actions = new ObservableCollection<RuleAction>(rule.Actions);
            ActionsList.ItemsSource = _actions;

            AdvancedModeCheckBox.IsChecked = RuleUsesAdvancedFeatures(rule);
            ApplyAdvancedMode();
        }

        private void AdvancedModeCheckBox_Changed(object sender, RoutedEventArgs e) => ApplyAdvancedMode();

        private void ConditionsHelpToggle_Changed(object sender, RoutedEventArgs e)
        {
            ConditionsHelpPanel.Visibility = ConditionsHelpToggle.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        }

        private void ApplyAdvancedMode()
        {
            bool advanced = AdvancedModeCheckBox.IsChecked == true;
            VariableHelpPanel.Visibility = advanced ? Visibility.Visible : Visibility.Collapsed;
            FieldComboBox.ItemsSource = advanced ? AllFields : BasicFields;
            ActionTypeComboBox.ItemsSource = advanced ? AllActions : BasicActions;

            if (_selectedNode?.NodeType == ConditionNodeType.Leaf)
            {
                _suppressEvents = true;
                ConditionOperator[] choices = GetOperatorChoices(_selectedNode.Field);
                OperatorComboBox.ItemsSource = choices;
                OperatorComboBox.SelectedItem = choices.Contains(_selectedNode.Operator) ? (object)_selectedNode.Operator : choices.FirstOrDefault();
                _suppressEvents = false;
            }
            else
            {
                OperatorComboBox.ItemsSource = advanced ? AllOperators : BasicOperators;
            }
        }

        private ConditionOperator[] GetOperatorChoices(ConditionField field)
        {
            ConditionOperator[] allowedForField = ConditionOperatorCatalog.SupportedOperators(field);
            bool advanced = AdvancedModeCheckBox.IsChecked == true;
            ConditionOperator[] allowedForMode = advanced ? AllOperators : BasicOperators;
            return allowedForField.Where(op => allowedForMode.Contains(op)).ToArray();
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

        private static bool IsDateField(ConditionField field) =>
            field == ConditionField.CreatedDate || field == ConditionField.ModifiedDate || field == ConditionField.AccessedDate;

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
                    OperatorComboBox.ItemsSource = GetOperatorChoices(_selectedNode.Field);
                    OperatorComboBox.SelectedItem = _selectedNode.Operator;
                    ValueTextBox.Text = _selectedNode.Value;
                    CaseSensitiveCheckBox.IsChecked = _selectedNode.CaseSensitive;
                    DuplicateHintText.Visibility = _selectedNode.Field == ConditionField.Duplicate ? Visibility.Visible : Visibility.Collapsed;
                    UpdateDateValueUi(_selectedNode);
                }
            }
            finally
            {
                _suppressEvents = false;
            }
        }

        private void UpdateDateValueUi(ConditionNode node)
        {
            bool isAll = node.Field == ConditionField.All;
            bool isDate = IsDateField(node.Field);

            AllFieldHintText.Visibility = isAll ? Visibility.Visible : Visibility.Collapsed;
            OperatorRow.Visibility = isAll ? Visibility.Collapsed : Visibility.Visible;
            CaseSensitiveCheckBox.Visibility = isAll ? Visibility.Collapsed : Visibility.Visible;
            TextValuePanel.Visibility = !isAll && !isDate ? Visibility.Visible : Visibility.Collapsed;
            DateValuePanel.Visibility = !isAll && isDate ? Visibility.Visible : Visibility.Collapsed;

            if (!isDate)
            {
                return;
            }

            bool isRange = node.Operator == ConditionOperator.Between;
            DateToLabel.Visibility = isRange ? Visibility.Visible : Visibility.Collapsed;
            DateToPicker.Visibility = isRange ? Visibility.Visible : Visibility.Collapsed;

            string[] parts = (node.Value ?? string.Empty).Split(',');
            DateFromPicker.SelectedDate = TryParse(parts.ElementAtOrDefault(0));
            DateToPicker.SelectedDate = TryParse(parts.ElementAtOrDefault(1));
        }

        private static DateTime? TryParse(string value) =>
            !string.IsNullOrWhiteSpace(value) && DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime result)
                ? result
                : (DateTime?)null;

        private void DatePicker_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (_suppressEvents || _selectedNode == null || !IsDateField(_selectedNode.Field))
            {
                return;
            }

            string from = DateFromPicker.SelectedDate?.ToString("yyyy-MM-dd") ?? string.Empty;
            bool isRange = _selectedNode.Operator == ConditionOperator.Between;

            _selectedNode.Value = isRange
                ? $"{from},{DateToPicker.SelectedDate?.ToString("yyyy-MM-dd")}"
                : from;

            ValueTextBox.Text = _selectedNode.Value;
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
                var newField = (ConditionField)FieldComboBox.SelectedItem;
                bool fieldChanged = newField != _selectedNode.Field;
                _selectedNode.Field = newField;
                DuplicateHintText.Visibility = _selectedNode.Field == ConditionField.Duplicate ? Visibility.Visible : Visibility.Collapsed;

                if (fieldChanged)
                {
                    // The old operator may not be valid for the new field (this is exactly how a
                    // hand-edited rule.json can end up with e.g. Extension + GreaterThan) — reset it.
                    _suppressEvents = true;
                    ConditionOperator[] choices = GetOperatorChoices(newField);
                    OperatorComboBox.ItemsSource = choices;
                    OperatorComboBox.SelectedItem = choices.Contains(_selectedNode.Operator) ? (object)_selectedNode.Operator : choices.FirstOrDefault();
                    _suppressEvents = false;
                    _selectedNode.Operator = OperatorComboBox.SelectedItem is ConditionOperator selected ? selected : default;
                    UpdateDateValueUi(_selectedNode);
                }
            }
            if (OperatorComboBox.SelectedItem != null)
            {
                _selectedNode.Operator = (ConditionOperator)OperatorComboBox.SelectedItem;
                UpdateDateValueUi(_selectedNode);
            }

            if (!IsDateField(_selectedNode.Field) && _selectedNode.Field != ConditionField.All)
            {
                _selectedNode.Value = ValueTextBox.Text;
            }
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

        private void ActionsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            _selectedAction = ActionsList.SelectedItem as RuleAction;
            _suppressEvents = true;
            try
            {
                if (_selectedAction == null)
                {
                    NoActionSelectedText.Visibility = Visibility.Visible;
                    ActionFieldsPanel.Visibility = Visibility.Collapsed;
                    return;
                }

                NoActionSelectedText.Visibility = Visibility.Collapsed;
                ActionFieldsPanel.Visibility = Visibility.Visible;

                ActionTypeComboBox.SelectedItem = _selectedAction.Type;
                DestinationTextBox.Text = _selectedAction.Destination;
                ArgumentsTextBox.Text = _selectedAction.Arguments;
                OnConflictComboBox.SelectedItem = _selectedAction.OnConflict;

                ApplyActionTypeUi(_selectedAction.Type);
            }
            finally
            {
                _suppressEvents = false;
            }
        }

        private void ApplyActionTypeUi(ActionType type)
        {
            bool needsDestination = type != ActionType.DeleteToRecycleBin && type != ActionType.Open && type != ActionType.RemoveExtension;
            bool needsArguments = type == ActionType.ExecuteExternal;
            bool needsConflict = type == ActionType.Move || type == ActionType.Copy || type == ActionType.Rename ||
                                  type == ActionType.AddExtension || type == ActionType.RemoveExtension;

            DestinationPanel.Visibility = needsDestination ? Visibility.Visible : Visibility.Collapsed;
            ArgumentsPanel.Visibility = needsArguments ? Visibility.Visible : Visibility.Collapsed;
            OnConflictPanel.Visibility = needsConflict ? Visibility.Visible : Visibility.Collapsed;

            DestinationLabel.Text = type == ActionType.Rename ? "New name:" :
                                     type == ActionType.AddExtension ? "Extension:" :
                                     type == ActionType.OpenWith || type == ActionType.ExecuteExternal ? "Program:" :
                                     "Destination:";

            ActionHelpText.Text = ActionHelp.TryGetValue(type, out string help) ? help : string.Empty;
        }

        private void ActionType_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (_suppressEvents || _selectedAction == null || ActionTypeComboBox.SelectedItem == null)
            {
                return;
            }
            _selectedAction.Type = (ActionType)ActionTypeComboBox.SelectedItem;
            ApplyActionTypeUi(_selectedAction.Type);
        }

        private void ActionField_Changed(object sender, RoutedEventArgs e)
        {
            if (_suppressEvents || _selectedAction == null)
            {
                return;
            }
            _selectedAction.Destination = DestinationTextBox.Text;
            _selectedAction.Arguments = ArgumentsTextBox.Text;
            if (OnConflictComboBox.SelectedItem != null)
            {
                _selectedAction.OnConflict = (ConflictResolution)OnConflictComboBox.SelectedItem;
            }
        }

        private void InsertDestinationVariable_Click(object sender, RoutedEventArgs e) =>
            ShowVariableMenu((Button)sender, DestinationTextBox);

        private void InsertArgumentsVariable_Click(object sender, RoutedEventArgs e) =>
            ShowVariableMenu((Button)sender, ArgumentsTextBox);

        private void ShowVariableMenu(Button anchor, TextBox target)
        {
            var menu = new ContextMenu();
            foreach ((string group, string[] tokens) in VariableGroups)
            {
                var groupItem = new MenuItem { Header = group };
                foreach (string token in tokens)
                {
                    if (token == null)
                    {
                        groupItem.Items.Add(new Separator());
                        continue;
                    }

                    var item = new MenuItem { Header = token };
                    item.Click += (s, e) => InsertAtCursor(target, token);
                    groupItem.Items.Add(item);
                }
                menu.Items.Add(groupItem);
            }
            anchor.ContextMenu = menu;
            menu.PlacementTarget = anchor;
            menu.IsOpen = true;
        }

        private void InsertAtCursor(TextBox textBox, string token)
        {
            int caret = textBox.CaretIndex;
            textBox.Text = (textBox.Text ?? string.Empty).Insert(caret, token);
            textBox.CaretIndex = caret + token.Length;
            textBox.Focus();
            // TextChanged already wired to ActionField_Changed, so the model updates as normal.
        }

        private void AddAction_Click(object sender, RoutedEventArgs e)
        {
            var action = new RuleAction { Type = ActionType.Move };
            _actions.Add(action);
            ActionsList.SelectedItem = action;
        }

        private void RemoveAction_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedAction != null)
            {
                _actions.Remove(_selectedAction);
            }
        }

        private void MoveActionUp_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedAction == null)
            {
                return;
            }
            int index = _actions.IndexOf(_selectedAction);
            if (index > 0)
            {
                _actions.Move(index, index - 1);
            }
        }

        private void MoveActionDown_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedAction == null)
            {
                return;
            }
            int index = _actions.IndexOf(_selectedAction);
            if (index >= 0 && index < _actions.Count - 1)
            {
                _actions.Move(index, index + 1);
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

            var candidate = new Rule
            {
                Name = NameTextBox.Text.Trim(),
                Enabled = EnabledCheckBox.IsChecked == true,
                RootCondition = _rule.RootCondition,
                Actions = _actions
            };

            List<string> issues = RuleValidator.Validate(candidate);
            if (issues.Count > 0)
            {
                MessageBoxResult proceed = MessageBox.Show(this,
                    "Er zijn problemen gevonden met deze regel:\n\n- " + string.Join("\n- ", issues) +
                    "\n\nToch opslaan?",
                    "FldrSrtr", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                if (proceed != MessageBoxResult.Yes)
                {
                    return;
                }
            }

            _rule.Name = candidate.Name;
            _rule.Enabled = candidate.Enabled;

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
