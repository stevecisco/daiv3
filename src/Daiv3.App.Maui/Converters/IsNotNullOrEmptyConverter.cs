using System.Collections;
using System.Globalization;

namespace Daiv3.App.Maui.Converters;

/// <summary>
/// Converter that returns true if the value is a non-null, non-empty collection or string.
/// Used to control visibility of UI elements based on data availability.
/// </summary>
public class IsNotNullOrEmptyConverter : IValueConverter
{
    /// <summary>
    /// Converts a collection or string value to a boolean indicating if it is non-null and non-empty.
    /// </summary>
    /// <param name="value">The collection or string value to check.</param>
    /// <param name="targetType">The target type (bool).</param>
    /// <param name="parameter">Not used.</parameter>
    /// <param name="culture">Not used.</param>
    /// <returns>True if value is non-null and non-empty; false otherwise.</returns>
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value switch
        {
            null => false,
            string str => !string.IsNullOrEmpty(str),
            ICollection collection => collection.Count > 0,
            IEnumerable enumerable => enumerable.Cast<object>().Any(),
            _ => true
        };
    }

    /// <summary>
    /// Conversion back is not supported for this converter.
    /// </summary>
    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
