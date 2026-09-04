using System;
using System.Globalization;
using System.Windows.Data;

namespace FldrSrtr
{
    /// <summary>True when the bound string is set — used to enable a "Reset" button only once an override exists.</summary>
    public class IsNotNullOrEmptyConverter : IValueConverter
    {
        public static readonly IsNotNullOrEmptyConverter Instance = new IsNotNullOrEmptyConverter();

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
            value is string s && !string.IsNullOrEmpty(s);

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
            throw new NotSupportedException();
    }
}
