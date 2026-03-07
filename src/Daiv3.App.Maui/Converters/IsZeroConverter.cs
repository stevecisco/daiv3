using System.Globalization;

namespace Daiv3.App.Maui.Converters;

/// <summary>
/// Returns true when a numeric value is equal to zero.
/// </summary>
public sealed class IsZeroConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return GetNumericValue(value) == 0;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();

    private static double GetNumericValue(object? value)
    {
        if (value is null)
        {
            return 0;
        }

        if (value is int i)
        {
            return i;
        }

        if (value is long l)
        {
            return l;
        }

        if (value is float f)
        {
            return f;
        }

        if (value is double d)
        {
            return d;
        }

        if (value is decimal m)
        {
            return (double)m;
        }

        return 0;
    }
}
