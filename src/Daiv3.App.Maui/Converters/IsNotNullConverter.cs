using System.Globalization;

namespace Daiv3.App.Maui.Converters;

/// <summary>
/// Returns true when the bound value is not null.
/// </summary>
public sealed class IsNotNullConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is not null;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
