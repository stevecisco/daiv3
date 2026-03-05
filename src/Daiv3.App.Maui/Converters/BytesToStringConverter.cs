using System.Globalization;

namespace Daiv3.App.Maui.Converters;

/// <summary>
/// Converts byte counts into readable size strings.
/// </summary>
public sealed class BytesToStringConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is null)
        {
            return "0 B";
        }

        long bytes;
        try
        {
            bytes = System.Convert.ToInt64(value, culture);
        }
        catch
        {
            return "0 B";
        }

        if (bytes < 0)
        {
            bytes = 0;
        }

        const long kb = 1024;
        const long mb = kb * 1024;
        const long gb = mb * 1024;

        if (bytes >= gb)
        {
            return $"{bytes / (double)gb:F2} GB";
        }

        if (bytes >= mb)
        {
            return $"{bytes / (double)mb:F2} MB";
        }

        if (bytes >= kb)
        {
            return $"{bytes / (double)kb:F2} KB";
        }

        return $"{bytes} B";
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
