using System;
using System.Globalization;
using System.IO;
using System.Windows.Data;

namespace FldrSrtr
{
    /// <summary>
    /// Resolves a button's Tag (e.g. "Icons/icon-add.png" — the filename is all that matters, the
    /// folder prefix is historical and ignored) to an absolute file path. Checks, in order: a
    /// user-picked per-icon override (IconSetProvider.Overrides — someone's own file, doesn't even
    /// need to live under IconSets\), the currently active icon pack (IconSetProvider.BasePath),
    /// then the Default pack — so a partial override or a partial custom pack still renders
    /// everything else instead of going blank.
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

            if (IconSetProvider.Overrides.TryGetValue(fileName, out string overridePath) && File.Exists(overridePath))
            {
                return overridePath;
            }

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
