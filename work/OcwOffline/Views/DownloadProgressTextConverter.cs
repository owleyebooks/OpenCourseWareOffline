using System.Globalization;
using OcwOffline.Models;

namespace OcwOffline.Views;

/// <summary>
/// One text line under an active download row, e.g.
/// "Downloading… 38%, 340 of 900 MB". Multi-bound to the changing pieces
/// (status, progress, bytes) because a "." binding would not refresh as
/// the download ticks. Empty string when the row is not actively
/// downloading/paused, or when the total size is unknown, so the bound
/// Label collapses.
/// </summary>
public class DownloadProgressTextConverter : IMultiValueConverter
{
    public object Convert(object?[] values, Type targetType, object? parameter, CultureInfo culture)
    {
        if (values is not [DownloadStatus status, double progress, long bytesDownloaded, long fileSizeBytes])
            return string.Empty;

        if (status is not (DownloadStatus.InProgress or DownloadStatus.Paused))
            return string.Empty;
        if (fileSizeBytes <= 0)
            return string.Empty;

        var stateWord = status == DownloadStatus.InProgress ? "Downloading…" : "Paused…";
        var percent = (int)Math.Round(progress * 100);
        return $"{stateWord} {percent}%, {ByteSizeConverter.Format(bytesDownloaded)} of {ByteSizeConverter.Format(fileSizeBytes)}";
    }

    public object[] ConvertBack(object? value, Type[] targetTypes, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
