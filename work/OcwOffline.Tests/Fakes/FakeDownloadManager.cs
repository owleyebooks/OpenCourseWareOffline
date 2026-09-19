using OcwOffline.Services;

namespace OcwOffline.Tests.Fakes;

/// <summary>
/// In-memory stand-in for DownloadManager. Outcomes are scripted per
/// progressKey via QueueSuccess/QueueFailure instead of a real network call.
/// Members beyond IDownloadManager are for test setup/assertions.
/// </summary>
public class FakeDownloadManager : IDownloadManager
{
    private readonly Dictionary<string, long> _files = new();
    private readonly Dictionary<string, ScriptedOutcome> _scripts = new();
    private readonly Dictionary<string, Exception> _extractFailures = new();
    private readonly HashSet<string> _pausedKeys = new();

    // Consumed by the next DownloadAsync call for the key, unlike the
    // permanent _pausedKeys record. See DownloadAsync/Pause below.
    private readonly HashSet<string> _pendingPauses = new();

    public List<string> ExtractedZipDestinations { get; } = new();
    public List<string> DeletedExtractedContents { get; } = new();

    public event Action<DownloadProgress>? ProgressChanged;

    private record ScriptedOutcome(string? RelativePath, long SizeBytes, Exception? Exception);

    public void QueueSuccess(string progressKey, long sizeBytes, string? relativePath = null) =>
        _scripts[progressKey] = new ScriptedOutcome(relativePath, sizeBytes, null);

    public void QueueFailure(string progressKey, Exception exception) =>
        _scripts[progressKey] = new ScriptedOutcome(null, 0, exception);

    // Keyed by destinationSubfolder, not progressKey. ExtractZipAsync's own
    // signature has no progress key. Consumed on use.
    public void QueueExtractFailure(string destinationSubfolder, Exception exception) =>
        _extractFailures[destinationSubfolder] = exception;

    public bool WasPaused(string progressKey) => _pausedKeys.Contains(progressKey);

    public Task<string> DownloadAsync(
        string sourceUrl,
        string subfolder,
        string fileName,
        string progressKey,
        CancellationToken externalToken = default)
    {
        externalToken.ThrowIfCancellationRequested();

        // An unconsumed Pause() for this key cancels this attempt instead
        // of running the scripted outcome, like a live cts.Cancel() would.
        // WasPaused() stays true regardless.
        if (_pendingPauses.Remove(progressKey))
            throw new OperationCanceledException(
                $"Simulated Pause() of '{progressKey}': matches DownloadManager.Pause() cancelling an in-flight download.");

        if (!_scripts.Remove(progressKey, out var outcome))
            throw new InvalidOperationException(
                $"No scripted outcome queued for '{progressKey}': call QueueSuccess/QueueFailure first.");

        if (outcome.Exception is not null)
            throw outcome.Exception;

        var relativePath = outcome.RelativePath ?? Path.Combine(subfolder, fileName);
        _files[relativePath] = outcome.SizeBytes;

        // Mirrors the real DownloadManager firing ProgressChanged more than
        // once per download instead of only the final 100%. A listener
        // that only reacts to the last event still sees Fraction 1.0.
        if (outcome.SizeBytes > 0)
            ProgressChanged?.Invoke(new DownloadProgress(progressKey, outcome.SizeBytes / 2, outcome.SizeBytes));
        ProgressChanged?.Invoke(new DownloadProgress(progressKey, outcome.SizeBytes, outcome.SizeBytes));

        return Task.FromResult(relativePath);
    }

    public void Pause(string progressKey)
    {
        _pausedKeys.Add(progressKey);
        _pendingPauses.Add(progressKey);
    }

    public Task ExtractZipAsync(string zipPath, string destinationSubfolder)
    {
        if (_extractFailures.Remove(destinationSubfolder, out var exception))
            throw exception;

        ExtractedZipDestinations.Add(destinationSubfolder);
        return Task.CompletedTask;
    }

    public Task<long> GetTotalStorageUsedAsync() =>
        Task.FromResult(_files.Values.Sum());

    public Task<long> GetStorageUsedByCourseAsync(string courseId) =>
        Task.FromResult(_files.Where(f => CourseIdOf(f.Key) == courseId).Sum(f => f.Value));

    public void DeleteFile(string relativePath) => _files.Remove(relativePath);

    public void DeleteCourseFolder(string courseId)
    {
        foreach (var key in _files.Keys.Where(k => CourseIdOf(k) == courseId).ToList())
            _files.Remove(key);
    }

    public void DeleteExtractedContents(string courseId, int artifactId) =>
        DeletedExtractedContents.Add(Path.Combine(courseId, $"_extracted_{artifactId}"));

    // Mirrors the real DownloadManager's own convention: subfolder/courseId
    // is always the first path segment of any relative path it hands back.
    private static string CourseIdOf(string relativePath) =>
        relativePath.Split('/', '\\')[0];
}
