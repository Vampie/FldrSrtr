using System;
using System.Globalization;
using System.Windows.Data;
using App.Core.Model;

namespace FldrSrtr
{
    public class ConditionNodeLabelConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (!(value is ConditionNode node))
            {
                return string.Empty;
            }

            return node.NodeType == ConditionNodeType.Group
                ? node.GroupLogic.ToString().ToUpperInvariant()
                : $"{node.Field} {node.Operator} \"{node.Value}\"" + (node.CaseSensitive ? " (case-sensitive)" : string.Empty);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
            throw new NotSupportedException();
    }
}
