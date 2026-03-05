using Daiv3.Knowledge;
using System.Globalization;

namespace Daiv3.App.Maui.Converters;

/// <summary>
/// Converts file indexing status values into glyph icons for quick visual scanning.
/// </summary>
public sealed class StatusIconConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not FileIndexingStatus status)
        {
            return "?";
        }

        return status switch
        {
            FileIndexingStatus.Indexed => "✓",
            FileIndexingStatus.InProgress => "⟳",
            FileIndexingStatus.Warning => "!",
            FileIndexingStatus.Error => "X",
            FileIndexingStatus.NotIndexed => "○",
            _ => "?"
        };
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
