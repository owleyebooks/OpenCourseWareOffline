using System.Globalization;

namespace OcwOffline.Views;

/// <summary>True when the bound long is positive. Hides the row's size
/// label when FileSizeBytes is 0 (unknown).</summary>
public class LongIsPositiveConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is long bytes && bytes > 0;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
