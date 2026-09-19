using OcwOffline.Models;

namespace OcwOffline.Services;

/// <summary>
/// OcwCatalogService's public surface: lets consumers depend on this
/// instead of the concrete HttpClient-backed class. Same shape as
/// ICourseDatabase/IDownloadManager/IOcwScraperService.
/// </summary>
public interface IOcwCatalogService
{
    Task<(List<CatalogEntry> Entries, bool HasMore)> GetCoursePageAsync(int offset, int limit = 20, CancellationToken ct = default);
}
