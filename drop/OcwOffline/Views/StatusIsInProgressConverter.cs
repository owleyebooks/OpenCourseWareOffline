using System.Globalization;
using OcwOffline.Models;

namespace OcwOffline.Views;

/// <summary>
/// Pause only makes sense while InProgress (Extracting has already lost
/// its CancellationTokenSource). Kept separate from
/// StatusIsActiveConverter so the progress bar's behavior can't drift. See AUDIT_TRAIL v13.
/// </summary>
public class StatusIsInProgressConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is DownloadStatus status && status == DownloadStatus.InProgress;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
