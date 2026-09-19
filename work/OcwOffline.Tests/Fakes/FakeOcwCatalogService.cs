using OcwOffline.Models;
using OcwOffline.Services;

namespace OcwOffline.Tests.Fakes;

/// <summary>
/// In-memory stand-in for OcwCatalogService. Outcomes are scripted per
/// offset via QueueResult/QueueFailure instead of a real HTTP fetch.
/// Same consumed-once-per-call shape as FakeOcwScraperService.
/// </summary>
public class FakeOcwCatalogService : IOcwCatalogService
{
    private readonly Dictionary<int, (List<CatalogEntry> Entries, bool HasMore)> _results = new();
    private readonly Dictionary<int, Exception> _failures = new();

    public List<int> RequestedOffsets { get; } = new();

    public void QueueResult(int offset, List<CatalogEntry> entries, bool hasMore) =>
        _results[offset] = (entries, hasMore);

    public void QueueFailure(int offset, Exception exception) =>
        _failures[offset] = exception;

    public Task<(List<CatalogEntry> Entries, bool HasMore)> GetCoursePageAsync(int offset, int limit = 20, CancellationToken ct = default)
    {
        RequestedOffsets.Add(offset);

        if (_failures.Remove(offset, out var exception))
            throw exception;

        if (!_results.Remove(offset, out var result))
            throw new InvalidOperationException(
                $"No scripted result queued for offset {offset}: call QueueResult/QueueFailure first.");

        return Task.FromResult(result);
    }
}
