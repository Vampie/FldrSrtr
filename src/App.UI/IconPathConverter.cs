using System;
using System.Globalization;
using System.IO;
using System.Windows.Data;

namespace FldrSrtr
{
    /// <summary>
    /// Resolves a button's Tag (e.g. "Icons/icon-add.png" — the filename is all that matters, the
    /// folder prefix is historical and ignored) to an absolute file path in the currently active
    /// icon pack (IconSetProvider.BasePath). Falls back to the Default pack when the active pack
    /// doesn't have that specific file, so a partial custom pack — someone overriding just a few
    /// icons — still renders everything else instead of going blank.
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

            string fileName = Path.GetFileName(path);
            string inActiveSet = Path.Combine(IconSetProvider.BasePath, fileName);
            if (File.Exists(inActiveSet))
            {
                return inActiveSet;
            }

            string inDefaultSet = Path.Combine(IconSetProvider.DefaultSetFolder, fileName);
            return File.Exists(inDefaultSet) ? inDefaultSet : null;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
            throw new NotSupportedException();
    }
}
