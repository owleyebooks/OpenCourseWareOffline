using System.Globalization;
using OcwOffline.Models;

namespace OcwOffline.Views;

/// <summary>Progress bar only makes sense while a row is actively downloading or extracting.</summary>
public class StatusIsActiveConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is DownloadStatus status &&
           (status == DownloadStatus.InProgress || status == DownloadStatus.Extracting);

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
