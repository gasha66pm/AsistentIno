using System.Globalization;
using System.IO;
using System.Windows.Data;

namespace AsistentIno.Converters;

public class FileNameConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not string path || string.IsNullOrWhiteSpace(path))
            return string.Empty;

        return Path.GetFileName(path.TrimEnd('/', '\\'));
    }

    public object ConvertBack(object? value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
