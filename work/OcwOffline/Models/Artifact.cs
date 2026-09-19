using CommunityToolkit.Mvvm.ComponentModel;
using SQLite;

namespace OcwOffline.Models;

public enum ArtifactFileType
{
    Pdf,
    Html,
    Zip,
    Other
}

public enum DownloadStatus
{
    NotStarted,
    InProgress,
    Paused,
    Completed,
    Extracting, // zip only: between download completing and extraction finishing
    Failed
}

/// <summary>
/// ObservableObject so CollectionView rows update live during download.
/// sqlite-net-pcl still persists fine via [ObservableProperty]'s
/// generated get/set.
/// </summary>
public partial class Artifact : ObservableObject
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [Indexed]
    public string CourseId { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;
    public ArtifactFileType FileType { get; set; }
    public string SourceUrl { get; set; } = string.Empty;

    // Null until downloaded. Relative to the app Documents/ root so
    // it survives app reinstalls/path changes across OS updates.
    [ObservableProperty]
    private string? localFilePath;

    [ObservableProperty]
    private DownloadStatus downloadStatus = DownloadStatus.NotStarted;

    public long FileSizeBytes { get; set; }

    [ObservableProperty]
    private long bytesDownloaded; // for resumable progress display

    /// <summary>0.0-1.0, bound directly to a ProgressBar in the row template.</summary>
    [ObservableProperty]
    private double progress;

    /// <summary>Set once ExtractZip() has unpacked this artifact (zip-type only).</summary>
    [ObservableProperty]
    private bool isExtracted;

    // Not persisted (get-only, so sqlite-net-pcl skips it). Drives the
    // CoursePage "View" button.
    public bool IsViewable => FileType is ArtifactFileType.Pdf or ArtifactFileType.Html
        && !string.IsNullOrEmpty(LocalFilePath);

    // Manual OnLocalFilePathChanged hook so IsViewable's own change
    // notifies the UI.
    partial void OnLocalFilePathChanged(string? value) => OnPropertyChanged(nameof(IsViewable));
}
