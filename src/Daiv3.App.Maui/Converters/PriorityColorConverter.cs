using System.Globalization;

namespace Daiv3.App.Maui.Converters;

/// <summary>
/// Converter that maps queue item priority levels to color codes for visual highlighting.
/// Implements color-coding per CT-REQ-004 for priority-level visualization.
/// </summary>
public class PriorityColorConverter : IValueConverter
{
    /// <summary>
    /// Converts a priority string to a Color for visual highlighting.
    /// Priority levels are color-coded:
    /// - "Immediate" (P0) → Red (#EF4444)
    /// - "High" / "Urgent" → Orange (#F97316)
    /// - "Normal" (P1) → Blue (#3B82F6)
    /// - "Background" (P2) → Gray (#6B7280)
    /// </summary>
    /// <param name="value">The priority string value (e.g., "Immediate", "Normal", "Background").</param>
    /// <param name="targetType">The target type (Color).</param>
    /// <param name="parameter">Not used.</param>
    /// <param name="culture">Not used.</param>
    /// <returns>A Color corresponding to the priority level.</returns>
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not string priority)
            return Colors.Gray; // Default fallback color

        return priority.ToLowerInvariant() switch
        {
            "immediate" or "p0" or "critical" => Color.FromArgb("#EF4444"), // Red
            "urgent" or "high" => Color.FromArgb("#F97316"),                 // Orange
            "normal" or "p1" or "default" => Color.FromArgb("#3B82F6"),     // Blue
            "background" or "p2" or "low" => Color.FromArgb("#6B7280"),     // Gray
            _ => Color.FromArgb("#6B7280")                                   // Gray default
        };
    }

    /// <summary>
    /// Conversion back is not supported for this converter.
    /// </summary>
    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
