using System.Globalization;

namespace Daiv3.App.Maui.Converters;

/// <summary>
/// Converts a read/unread flag to a neutral background color.
/// </summary>
public sealed class BoolToBackgroundColorConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool isRead && isRead)
        {
            return Color.FromArgb("#F5F5F5");
        }

        return Color.FromArgb("#FFFFFF");
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
