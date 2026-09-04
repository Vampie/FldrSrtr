using System;
using System.Globalization;
using System.Windows.Data;

namespace FldrSrtr
{
    /// <summary>
    /// Rewrites a button's Tag (e.g. "Icons/icon-add.png", always written against the default
    /// set in XAML) to the currently active icon set's folder — so every IconButton's XAML stays
    /// untouched regardless of which set (Default/Slim) is selected in Settings.
    /// </summary>
    public class IconPathConverter : IValueConverter
    {
        public static readonly IconPathConverter Instance = new IconPathConverter();

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (!(value is string path) || string.IsNullOrEmpty(path))
            {
                return null;
            }

            return IconSetProvider.BasePath == IconSetProvider.DefaultFolder
                ? path
                : path.Replace(IconSetProvider.DefaultFolder, IconSetProvider.BasePath);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
            throw new NotSupportedException();
    }
}
