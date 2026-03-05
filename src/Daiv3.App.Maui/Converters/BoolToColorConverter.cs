using System.Globalization;

namespace Daiv3.App.Maui.Converters;

/// <summary>
/// Converts boolean values to success/neutral colors.
/// </summary>
public sealed class BoolToColorConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool flag && flag)
        {
            return Color.FromArgb("#16A34A");
        }

        return Color.FromArgb("#64748B");
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
