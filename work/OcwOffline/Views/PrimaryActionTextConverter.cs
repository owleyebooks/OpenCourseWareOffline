using System.Globalization;
using OcwOffline.Models;

namespace OcwOffline.Views;

/// <summary>
/// One stateful label for a download row's primary button. Multi-bound to
/// the item and its DownloadStatus: a plain "." binding would not refresh
/// when the status changes, so both ride along and either change
/// re-evaluates the text. Download, Pause, Resume, Retry, and Open/Watch
/// all flow through here so the row template needs no per-state buttons.
/// </summary>
public class PrimaryActionTextConverter : IMultiValueConverter
{
    public object Convert(object?[] values, Type targetType, object? parameter, CultureInfo culture)
    {
        return values switch
        {
            [Artifact artifact, ..] => ArtifactText(artifact),
            [Lecture lecture, ..] => LectureText(lecture),
            _ => string.Empty
        };
    }

    private static string ArtifactText(Artifact artifact) =>
        artifact.DownloadStatus switch
        {
            DownloadStatus.Extracting => "Working…",
            DownloadStatus.NotStarted => "Download",
            DownloadStatus.InProgress => "Pause",
            DownloadStatus.Paused => "Resume",
            DownloadStatus.Failed => "Retry",
            DownloadStatus.Completed => "Open",
            _ => string.Empty
        };

    private static string LectureText(Lecture lecture) =>
        lecture.DownloadStatus switch
        {
            DownloadStatus.Extracting => "Working…",
            DownloadStatus.NotStarted => "Download",
            DownloadStatus.InProgress => "Pause",
            DownloadStatus.Paused => "Resume",
            DownloadStatus.Failed => "Retry",
            DownloadStatus.Completed => lecture is { LastWatchedPositionSeconds: >= 30, IsCompleted: false }
                ? "Resume"
                : "Watch",
            _ => string.Empty
        };

    public object[] ConvertBack(object? value, Type[] targetTypes, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
