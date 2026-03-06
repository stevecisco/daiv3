using System.Globalization;

namespace Daiv3.App.Maui.Converters;

/// <summary>
/// Converts a percentage value (0-100) to a decimal value (0.0-1.0) for progress bars.
/// CT-REQ-011: Project Master Dashboard progress display.
/// </summary>
public class PercentToDecimalConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is double percent)
        {
            return percent / 100.0;
        }

        return 0.0;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is double decimalValue)
        {
            return decimalValue * 100.0;
        }

        return 0.0;
    }
}
