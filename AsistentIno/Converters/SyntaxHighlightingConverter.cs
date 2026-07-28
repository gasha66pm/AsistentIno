using System;
using System.Globalization;
using System.Windows.Data;
using ICSharpCode.AvalonEdit.Highlighting;

namespace AsistentIno.Converters;

public sealed class SyntaxHighlightingConverter : IValueConverter
{
    public object? Convert(
        object value,
        Type targetType,
        object? parameter,
        CultureInfo culture)
    {
        if (value is not bool enabled || !enabled)
            return null;

        return HighlightingManager.Instance.GetDefinition("C++");
    }

    public object ConvertBack(
        object value,
        Type targetType,
        object? parameter,
        CultureInfo culture) =>
        throw new NotSupportedException();
}