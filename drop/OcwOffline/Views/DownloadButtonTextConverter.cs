using System.Globalization;
using OcwOffline.Models;

namespace OcwOffline.Views;

/// <summary>Completed/Failed read "Re-download" so tapping doesn't look
/// like a no-op; other statuses pass through unchanged. See AUDIT_TRAIL v25.</summary>
public class DownloadButtonTextConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is DownloadStatus.Completed or DownloadStatus.Failed)
            return "Re-download";
        return value?.ToString() ?? string.Empty;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
