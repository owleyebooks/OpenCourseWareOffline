namespace OcwOffline.Services;

/// <summary>
/// OcwScraperService's public surface: lets consumers depend on this
/// instead of the concrete HttpClient-backed class. Same shape as
/// ICourseDatabase/IDownloadManager.
/// </summary>
public interface IOcwScraperService
{
    Task<ScrapedCourse> ScrapeDownloadPageAsync(string courseId);
}
