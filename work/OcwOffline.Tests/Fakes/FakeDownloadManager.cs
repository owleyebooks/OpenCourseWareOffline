using OcwOffline.Models;
using OcwOffline.Services;

namespace OcwOffline.Tests.Fakes;

/// <summary>
/// In-memory stand-in for DownloadManager. Outcomes are scripted per
/// progressKey via QueueSuccess/QueueFailure instead of a real network call.
/// Item-level DownloadArtifactAsync/DownloadLectureAsync mirror the real
/// manager's state machine (InProgress to Completed/Paused/Failed, zip
/// extraction, cancel resets). Members beyond IDownloadManager are for
/// test setup/assertions. An optional ICourseDatabase receives the same
/// upserts the real manager performs.
/// </summary>
public class FakeDownloadManager : IDownloadManager
{
    private readonly ICourseDatabase? _db;
    private readonly Dictionary<string, long> _files = new();
    private readonly Dictionary<string, ScriptedOutcome> _scripts = new();
    private readonly Dictionary<string, Exception> _extractFailures = new();
    private readonly HashSet<string> _pausedKeys = new();
    private readonly Dictionary<string, DownloadProgress> _latest = new();

    // Consumed by the next DownloadAsync call for the key, unlike the
    // permanent _pausedKeys record. See DownloadAsync/Pause below.
    private readonly HashSet<string> _pendingPauses = new();
    private readonly HashSet<string> _cancelledKeys = new();

    public FakeDownloadManager(ICourseDatabase? db = null)
    {
        _db = db;
    }

    public List<string> ExtractedZipDestinations { get; } = new();
    public List<string> DeletedExtractedContents { get; } = new();
    public List<string> CancelledKeys { get; } = new();

    public event Action<DownloadProgress>? ProgressChanged;
    public event Action<DownloadAggregate>? AggregateChanged;

    private record ScriptedOutcome(string? RelativePath, long SizeBytes, Exception? Exception);

    public void QueueSuccess(string progressKey, long sizeBytes, string? relativePath = null) =>
        _scripts[progressKey] = new ScriptedOutcome(relativePath, sizeBytes, null);

    public void QueueFailure(string progressKey, Exception exception) =>
        _scripts[progressKey] = new ScriptedOutcome(null, 0, exception);

    // Keyed by destinationSubfolder, not progressKey. ExtractZipAsync's own
    // signature has no progress key. Consumed on use.
    public void QueueExtractFailure(string destinationSubfolder, Exception exception) =>
        _extractFailures[destinationSubfolder] = exception;

    // Fires the aggregate event with a hand-built roll-up, the same event
    // DownloadAsync/FireProgress raises internally, so AppStatusViewModel
    // tests can drive exact counts and fractions.
    public void RaiseAggregate(DownloadAggregate aggregate) =>
        AggregateChanged?.Invoke(aggregate);

    // Lets a test plant a partial file that Cancel must delete, mirroring
    // bytes left on disk by an interrupted real download.
    public void SimulatePartialFile(string relativePath, long bytes) =>
        _files[relativePath] = bytes;

    public bool WasPaused(string progressKey) => _pausedKeys.Contains(progressKey);

    public IReadOnlyList<string> StoredFiles => _files.Keys.ToList();

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
            FireProgress(new DownloadProgress(progressKey, outcome.SizeBytes / 2, outcome.SizeBytes));
        FireProgress(new DownloadProgress(progressKey, outcome.SizeBytes, outcome.SizeBytes));

        return Task.FromResult(relativePath);
    }

    public async Task DownloadArtifactAsync(Artifact artifact)
    {
        var key = $"artifact-{artifact.Id}";
        await Task.Yield();

        if (_cancelledKeys.Remove(key))
        {
            ResetArtifact(artifact);
            await UpsertArtifactAsync(artifact);
            FireAggregate(key, null);
            return;
        }

        if (_pendingPauses.Remove(key))
        {
            artifact.DownloadStatus = DownloadStatus.Paused;
            await UpsertArtifactAsync(artifact);
            FireAggregate(key, null);
            return;
        }

        if (!_scripts.Remove(key, out var outcome))
            throw new InvalidOperationException(
                $"No scripted outcome queued for '{key}': call QueueSuccess/QueueFailure first.");

        if (outcome.Exception is not null)
        {
            artifact.DownloadStatus = DownloadStatus.Failed;
            await UpsertArtifactAsync(artifact);
            FireAggregate(key, null);
            throw outcome.Exception;
        }

        artifact.DownloadStatus = DownloadStatus.InProgress;
        var relativePath = outcome.RelativePath
            ?? Path.Combine(artifact.CourseId, DefaultFileName(artifact.SourceUrl, "artifact", artifact.Id));
        _files[relativePath] = outcome.SizeBytes;

        if (outcome.SizeBytes > 0)
            FireProgress(new DownloadProgress(key, outcome.SizeBytes / 2, outcome.SizeBytes));
        FireProgress(new DownloadProgress(key, outcome.SizeBytes, outcome.SizeBytes));

        artifact.LocalFilePath = relativePath;
        artifact.BytesDownloaded = outcome.SizeBytes;
        artifact.Progress = 1;

        if (artifact.FileType == ArtifactFileType.Zip)
        {
            var destinationSubfolder = Path.Combine(artifact.CourseId, $"_extracted_{artifact.Id}");
            if (_extractFailures.Remove(destinationSubfolder, out _))
                artifact.IsExtracted = false;
            else
            {
                ExtractedZipDestinations.Add(destinationSubfolder);
                artifact.IsExtracted = true;
            }
        }

        artifact.DownloadStatus = DownloadStatus.Completed;
        await UpsertArtifactAsync(artifact);
        FireAggregate(key, null);
    }

    public async Task DownloadLectureAsync(Lecture lecture)
    {
        var key = $"lecture-{lecture.Id}";
        await Task.Yield();

        if (_cancelledKeys.Remove(key))
        {
            ResetLecture(lecture);
            await UpsertLectureAsync(lecture);
            FireAggregate(key, null);
            return;
        }

        if (_pendingPauses.Remove(key))
        {
            lecture.DownloadStatus = DownloadStatus.Paused;
            await UpsertLectureAsync(lecture);
            FireAggregate(key, null);
            return;
        }

        if (!_scripts.Remove(key, out var outcome))
            throw new InvalidOperationException(
                $"No scripted outcome queued for '{key}': call QueueSuccess/QueueFailure first.");

        if (outcome.Exception is not null)
        {
            lecture.DownloadStatus = DownloadStatus.Failed;
            await UpsertLectureAsync(lecture);
            FireAggregate(key, null);
            throw outcome.Exception;
        }

        lecture.DownloadStatus = DownloadStatus.InProgress;
        var relativePath = outcome.RelativePath
            ?? Path.Combine(lecture.CourseId, DefaultFileName(lecture.VideoUrl, "lecture", lecture.Id));
        _files[relativePath] = outcome.SizeBytes;

        if (outcome.SizeBytes > 0)
            FireProgress(new DownloadProgress(key, outcome.SizeBytes / 2, outcome.SizeBytes));
        FireProgress(new DownloadProgress(key, outcome.SizeBytes, outcome.SizeBytes));

        lecture.LocalVideoPath = relativePath;
        lecture.BytesDownloaded = outcome.SizeBytes;
        lecture.Progress = 1;
        lecture.DownloadStatus = DownloadStatus.Completed;
        await UpsertLectureAsync(lecture);
        FireAggregate(key, null);
    }

    public void Pause(string progressKey)
    {
        _pausedKeys.Add(progressKey);
        _pendingPauses.Add(progressKey);
    }

    public void Cancel(string progressKey)
    {
        CancelledKeys.Add(progressKey);
        _cancelledKeys.Add(progressKey);
        _pendingPauses.Remove(progressKey);

        // Deletes the partial file when the test scripted (or simulated)
        // one under this key's outcome path.
        if (_scripts.TryGetValue(progressKey, out var outcome) && outcome.RelativePath is not null)
            _files.Remove(outcome.RelativePath);
    }

    public IReadOnlyList<ActiveDownload> GetActiveDownloads() =>
        _latest.Values
            .Select(p => new ActiveDownload(p.Key, p.BytesReceived, p.TotalBytes))
            .ToList();

    public DownloadAggregate GetAggregate() =>
        DownloadAggregate.Compute(_latest.Values);

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

    private void FireProgress(DownloadProgress progress)
    {
        _latest[progress.Key] = progress;
        ProgressChanged?.Invoke(progress);
        AggregateChanged?.Invoke(DownloadAggregate.Compute(_latest.Values));
    }

    private void FireAggregate(string key, DownloadProgress? snapshot)
    {
        if (snapshot is null)
            _latest.Remove(key);
        else
            _latest[key] = snapshot;
        AggregateChanged?.Invoke(DownloadAggregate.Compute(_latest.Values));
    }

    private static void ResetArtifact(Artifact artifact)
    {
        artifact.LocalFilePath = null;
        artifact.IsExtracted = false;
        artifact.Progress = 0;
        artifact.BytesDownloaded = 0;
        artifact.DownloadStatus = DownloadStatus.NotStarted;
    }

    private static void ResetLecture(Lecture lecture)
    {
        lecture.LocalVideoPath = null;
        lecture.Progress = 0;
        lecture.BytesDownloaded = 0;
        lecture.DownloadStatus = DownloadStatus.NotStarted;
    }

    private Task UpsertArtifactAsync(Artifact artifact) =>
        _db?.UpsertArtifactAsync(artifact) ?? Task.CompletedTask;

    private Task UpsertLectureAsync(Lecture lecture) =>
        _db?.UpsertLectureAsync(lecture) ?? Task.CompletedTask;

    private static string DefaultFileName(string url, string prefix, int id)
    {
        try
        {
            var name = Path.GetFileName(new Uri(url).LocalPath);
            if (!string.IsNullOrEmpty(name))
                return name;
        }
        catch
        {
            // Not an absolute URI in the test; fall through to the default.
        }
        return $"{prefix}-{id}.bin";
    }

    // Mirrors the real DownloadManager's own convention: subfolder/courseId
    // is always the first path segment of any relative path it hands back.
    private static string CourseIdOf(string relativePath) =>
        relativePath.Split('/', '\\')[0];
}
