namespace OcwOffline.Services;

/// <summary>
/// DownloadManager's public surface, extracted so ViewModels can depend on
/// this instead of the concrete HttpClient/filesystem-backed class. Same
/// pattern as ICourseDatabase (v33). See AUDIT_TRAIL v34.
/// </summary>
public interface IDownloadManager
{
    event Action<DownloadProgress>? ProgressChanged;

    Task<string> DownloadAsync(
        string sourceUrl,
        string subfolder,
        string fileName,
        string progressKey,
        CancellationToken externalToken = default);

    void Pause(string progressKey);

    Task ExtractZipAsync(string zipPath, string destinationSubfolder);

    Task<long> GetTotalStorageUsedAsync();
    Task<long> GetStorageUsedByCourseAsync(string courseId);

    void DeleteFile(string relativePath);
    void DeleteCourseFolder(string courseId);
    void DeleteExtractedContents(string courseId, int artifactId);
}
