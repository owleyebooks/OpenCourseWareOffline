using System.Globalization;
using OcwOffline.Models;

namespace OcwOffline.Views;

/// <summary>Delete only makes sense once a row is fully downloaded.</summary>
public class StatusIsCompletedConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is DownloadStatus status && status == DownloadStatus.Completed;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
