namespace OcwOffline.Services;

/// <summary>
/// DownloadManager's public surface, extracted so ViewModels can depend on
/// this instead of the concrete HttpClient/filesystem-backed class. Same
/// pattern as ICourseDatabase (v33). See AUDIT_TRAIL v34.
/// </summary>
public interface IDownloadManager
{
    event Action<DownloadProgress>? ProgressChanged;

    // Aggregate roll-up of every in-flight download (count, weighted
    // overall progress), for the persistent banner. Fires on start, each
    // throttled progress tick, and completion/pause/cancel.
    event Action<DownloadAggregate>? AggregateChanged;

    Task<string> DownloadAsync(
        string sourceUrl,
        string subfolder,
        string fileName,
        string progressKey,
        CancellationToken externalToken = default);

    // Item-level orchestration: drives one Artifact/Lecture through
    // InProgress to Completed (extracting zips), Paused, or Failed,
    // updating the entity's progress live. Lets the Downloads dashboard
    // pause/resume/cancel without duplicating CourseViewModel's logic.
    Task DownloadArtifactAsync(Models.Artifact artifact);
    Task DownloadLectureAsync(Models.Lecture lecture);

    void Pause(string progressKey);

    // Stops the transfer and deletes the partial file, resetting the row
    // to NotStarted. No-op for an unknown key.
    void Cancel(string progressKey);

    // Latest per-key snapshots, for the dashboard's active section.
    IReadOnlyList<ActiveDownload> GetActiveDownloads();
    DownloadAggregate GetAggregate();

    Task ExtractZipAsync(string zipPath, string destinationSubfolder);

    Task<long> GetTotalStorageUsedAsync();
    Task<long> GetStorageUsedByCourseAsync(string courseId);

    void DeleteFile(string relativePath);
    void DeleteCourseFolder(string courseId);
    void DeleteExtractedContents(string courseId, int artifactId);
}
