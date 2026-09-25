using OcwOffline.Models;

namespace OcwOffline.Services;

/// <summary>
/// Pure logic extracted from VideoPlayerPage.xaml.cs, MAUI-free once
/// separated from Player/StatusLabel (the page's partial stays XAML-bound).
/// </summary>
public static class VideoPlaybackLogic
{
    /// <summary>Mirrors LoadLecture's three status-text branches.</summary>
    public static string DetermineLoadStatus(Lecture lecture, bool fileExists)
    {
        if (string.IsNullOrEmpty(lecture.LocalVideoPath))
        {
            return "No downloaded file for this lecture.";
        }

        if (!fileExists)
        {
            return "Downloaded file is missing on disk.";
        }

        return lecture.LastWatchedPositionSeconds >= 30 && !lecture.IsCompleted
            ? $"Resuming from {TimeSpan.FromSeconds(lecture.LastWatchedPositionSeconds):mm\\:ss}."
            : string.Empty;
    }

    /// <summary>
    /// Mirrors SaveProgressAsync's duration/completion write, returning
    /// (null, null) when there's no usable duration yet, matching the
    /// original's "only touch these fields if durationSeconds > 0" guard.
    /// </summary>
    public static (int? DurationSeconds, bool? IsCompleted) DetermineCompletionState(int positionSeconds, int durationSeconds)
    {
        if (durationSeconds <= 0)
        {
            return (null, null);
        }

        return (durationSeconds, positionSeconds >= durationSeconds - 5);
    }
}
