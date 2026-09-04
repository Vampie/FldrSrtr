using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace FldrSrtr
{
    /// <summary>Hides a button's icon Image entirely when Tag (the icon path) isn't set.</summary>
    public class NullToVisibilityConverter : IValueConverter
    {
        public static readonly NullToVisibilityConverter Instance = new NullToVisibilityConverter();

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
            value is string s && !string.IsNullOrEmpty(s) ? Visibility.Visible : Visibility.Collapsed;

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
            throw new NotSupportedException();
    }
}
