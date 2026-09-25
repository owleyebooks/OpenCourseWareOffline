using System.Globalization;

namespace OcwOffline.Views;

public class ByteSizeConverter : IValueConverter
{
    // 0 -> "0 bytes", 512 -> "512 bytes", 1536 -> "1.5 KB",
    // 5 MB -> "5 MB", 1.8 GB -> "1.8 GB". Whole units show no decimals.
    public static string Format(long bytes)
    {
        if (bytes < 1024) return $"{bytes} bytes";
        if (bytes < 1024L * 1024) return $"{bytes / 1024.0:0.#} KB";
        if (bytes < 1024L * 1024 * 1024) return $"{bytes / (1024.0 * 1024):0.#} MB";
        return $"{bytes / (1024.0 * 1024 * 1024):0.#} GB";
    }

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is long bytes ? Format(bytes) : string.Empty;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
