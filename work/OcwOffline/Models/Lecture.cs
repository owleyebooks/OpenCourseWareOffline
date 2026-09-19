using CommunityToolkit.Mvvm.ComponentModel;
using SQLite;

namespace OcwOffline.Models;

/// <summary>
/// Partial + ObservableObject for the same reason as Artifact: live
/// progress-bar/status binding in the CollectionView row template.
/// </summary>
public partial class Lecture : ObservableObject
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [Indexed]
    public string CourseId { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    // Directly linked .mp4 URL scraped from the course's /download/ or
    // /video_galleries/ page. OCW self-hosts most lecture video, so this
    // is a plain HTTPS file URL in the common case.
    public string VideoUrl { get; set; } = string.Empty;

    [ObservableProperty]
    private string? localVideoPath;

    [ObservableProperty]
    private DownloadStatus downloadStatus = DownloadStatus.NotStarted;

    public long FileSizeBytes { get; set; }

    [ObservableProperty]
    private long bytesDownloaded;

    /// <summary>0.0-1.0, bound directly to a ProgressBar in the row template.</summary>
    [ObservableProperty]
    private double progress;

    public int DurationSeconds { get; set; }
    public int LastWatchedPositionSeconds { get; set; }
    public bool IsCompleted { get; set; }
}
