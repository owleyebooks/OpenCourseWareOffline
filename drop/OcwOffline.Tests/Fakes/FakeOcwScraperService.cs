using OcwOffline.Services;

namespace OcwOffline.Tests.Fakes;

/// <summary>
/// In-memory stand-in for OcwScraperService — outcomes are scripted per
/// courseId via QueueResult/QueueFailure instead of a real HTTP fetch.
/// Same consumed-once-per-call shape as FakeDownloadManager. See AUDIT_TRAIL v36.
/// </summary>
public class FakeOcwScraperService : IOcwScraperService
{
    private readonly Dictionary<string, ScrapedCourse> _results = new();
    private readonly Dictionary<string, Exception> _failures = new();

    public List<string> RequestedCourseIds { get; } = new();

    public void QueueResult(string courseId, ScrapedCourse result) =>
        _results[courseId] = result;

    public void QueueFailure(string courseId, Exception exception) =>
        _failures[courseId] = exception;

    public Task<ScrapedCourse> ScrapeDownloadPageAsync(string courseId)
    {
        RequestedCourseIds.Add(courseId);

        if (_failures.Remove(courseId, out var exception))
            throw exception;

        if (!_results.Remove(courseId, out var result))
            throw new InvalidOperationException(
                $"No scripted result queued for '{courseId}' — call QueueResult/QueueFailure first.");

        return Task.FromResult(result);
    }
}
