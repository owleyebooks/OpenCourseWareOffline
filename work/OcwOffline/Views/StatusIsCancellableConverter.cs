using System.Globalization;
using OcwOffline.Models;

namespace OcwOffline.Views;

/// <summary>Cancel only makes sense while a row is downloading or paused
/// (Extracting has already lost its cancellation source, like Pause).</summary>
public class StatusIsCancellableConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is DownloadStatus status &&
           (status == DownloadStatus.InProgress || status == DownloadStatus.Paused);

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
