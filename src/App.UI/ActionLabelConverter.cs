using System;
using System.Globalization;
using System.Windows.Data;
using App.Core.Model;

namespace FldrSrtr
{
    public class ActionLabelConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (!(value is RuleAction action))
            {
                return string.Empty;
            }

            string detail = action.Type == ActionType.RemoveExtension || action.Type == ActionType.Open
                ? string.Empty
                : $" — {action.Destination}";

            return $"{action.Type}{detail}";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
            throw new NotSupportedException();
    }
}
