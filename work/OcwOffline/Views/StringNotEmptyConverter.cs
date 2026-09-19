using System.Globalization;

namespace OcwOffline.Views;

/// <summary>Bound to LocalFilePath/LocalVideoPath. Delete button only makes sense once something's on disk.</summary>
public class StringNotEmptyConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => !string.IsNullOrEmpty(value as string);

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
