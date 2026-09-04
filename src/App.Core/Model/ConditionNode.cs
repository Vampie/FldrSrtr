using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace App.Core.Model
{
    /// <summary>
    /// A node in a rule's condition tree. Tagged union rather than a class hierarchy: keeps
    /// JSON serialization plain (no TypeNameHandling) and keeps the WPF tree editor's binding
    /// simple. NodeType says which half of the fields is meaningful. Raises PropertyChanged so
    /// a bound TreeView label updates live as the user edits a node.
    /// </summary>
    public class ConditionNode : INotifyPropertyChanged
    {
        private ConditionNodeType _nodeType = ConditionNodeType.Leaf;
        private ConditionField _field;
        private ConditionOperator _operator;
        private string _value;
        private bool _caseSensitive;
        private GroupLogic _groupLogic = GroupLogic.All;

        public ConditionNodeType NodeType
        {
            get => _nodeType;
            set => SetField(ref _nodeType, value);
        }

        // --- Leaf fields ---
        public ConditionField Field
        {
            get => _field;
            set => SetField(ref _field, value);
        }

        public ConditionOperator Operator
        {
            get => _operator;
            set => SetField(ref _operator, value);
        }

        /// <summary>
        /// Raw value, interpreted per field: filename/extension = literal text (comma-separated
        /// for IsOneOf/IsNotOneOf), size = bytes, age = days, wildcard/regex = pattern.
        /// </summary>
        public string Value
        {
            get => _value;
            set => SetField(ref _value, value);
        }

        public bool CaseSensitive
        {
            get => _caseSensitive;
            set => SetField(ref _caseSensitive, value);
        }

        // --- Group fields ---
        public GroupLogic GroupLogic
        {
            get => _groupLogic;
            set => SetField(ref _groupLogic, value);
        }

        public ObservableCollection<ConditionNode> Children { get; set; } = new ObservableCollection<ConditionNode>();

        public event PropertyChangedEventHandler PropertyChanged;

        private void SetField<T>(ref T field, T value, [CallerMemberName] string propertyName = null)
        {
            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

            // The tree's label binds to the whole node (Text="{Binding Converter=...}", no Path) so
            // it can show one formatted string built from several fields — a WPF binding with an
            // empty path only refreshes on a PropertyChanged whose name is null/"", not on a named
            // property, so without this the label change (fixed bug: tree not updating until the
            // editor is closed and reopened) never reaches the TreeView.
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(string.Empty));
        }

        public static ConditionNode NewGroup(GroupLogic logic = GroupLogic.All) => new ConditionNode
        {
            NodeType = ConditionNodeType.Group,
            GroupLogic = logic
        };

        public static ConditionNode NewLeaf(ConditionField field, ConditionOperator op, string value) => new ConditionNode
        {
            NodeType = ConditionNodeType.Leaf,
            Field = field,
            Operator = op,
            Value = value
        };
    }
}
