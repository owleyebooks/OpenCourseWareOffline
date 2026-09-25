using System.Net;
using OcwOffline.Models;

namespace OcwOffline.Services;

public record DownloadProgress(string Key, long BytesReceived, long TotalBytes)
{
    public double Fraction => TotalBytes > 0 ? (double)BytesReceived / TotalBytes : 0;
}

/// <summary>
/// Watchdog cancellation, not Pause(): the same exception type would
/// otherwise conflate the two.
/// </summary>
public class DownloadStalledException(string message) : Exception(message);

/// <summary>
/// Resumable downloads to Documents/ (never Library/Caches, per Apple's
/// storage-eviction rules) with iOS backup-exclusion applied on landing.
/// Item-level DownloadArtifactAsync/DownloadLectureAsync orchestrate one
/// entity each (progress, pause, cancel, zip extraction); DownloadAsync
/// stays the raw transport primitive underneath. AggregateChanged rolls
/// every in-flight download into one banner-friendly snapshot.
/// </summary>
public class DownloadManager : IDownloadManager
{
    private readonly ICourseDatabase _db;
    private readonly IMainThreadDispatcher _mainThread;
    private readonly IAppPaths _appPaths;
    private readonly HttpClient _http;

    private readonly object _stateLock = new();
    private readonly Dictionary<string, CancellationTokenSource> _activeDownloads = new();
    private readonly Dictionary<string, DownloadProgress> _latestProgress = new();
    private readonly Dictionary<string, string> _pendingPaths = new();
    private readonly HashSet<string> _cancelledKeys = new();
    private readonly Dictionary<string, DownloadOutcome> _lastOutcome = new();

    // HttpClient's default 100s timeout fires mid-download on large videos
    // and gets mislabeled Paused by the caller's cancellation-catch.
    // Disabled in favor of this class's own cancellation.
    // Replaces the disabled HttpClient timeout with a narrower check: no
    // bytes at all for StallTimeout, not a ceiling on total download time.
    // A slow-but-flowing download never trips this.
    private static readonly TimeSpan StallTimeout = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan StallCheckInterval = TimeSpan.FromSeconds(5);

    public DownloadManager(
        ICourseDatabase db,
        IMainThreadDispatcher mainThread,
        IAppPaths appPaths,
        HttpMessageHandler? httpHandler = null)
    {
        _db = db;
        _mainThread = mainThread;
        _appPaths = appPaths;
        _http = new HttpClient(httpHandler ?? new HttpClientHandler())
        {
            Timeout = Timeout.InfiniteTimeSpan
        };
    }

    public event Action<DownloadProgress>? ProgressChanged;
    public event Action<DownloadAggregate>? AggregateChanged;

    /// <summary>
    /// Downloads a file to Documents/{subfolder}/{fileName}, resuming
    /// from any partial bytes already on disk. Returns the relative
    /// path (relative to AppPaths.Root) to store in the DB.
    /// </summary>
    public async Task<string> DownloadAsync(
        string sourceUrl,
        string subfolder,
        string fileName,
        string progressKey,
        CancellationToken externalToken = default)
    {
        var dir = Path.Combine(_appPaths.Root, subfolder);
        Directory.CreateDirectory(dir);
        var destPath = Path.Combine(dir, fileName);

        var cts = CancellationTokenSource.CreateLinkedTokenSource(externalToken);
        lock (_stateLock)
        {
            _activeDownloads[progressKey] = cts;
            _pendingPaths[progressKey] = destPath;
        }

        var lastProgressUtc = DateTime.UtcNow;
        var stalled = false;
        _ = Task.Run(async () =>
        {
            try
            {
                while (!cts.IsCancellationRequested)
                {
                    await Task.Delay(StallCheckInterval, cts.Token);
                    if (DateTime.UtcNow - lastProgressUtc >= StallTimeout)
                    {
                        stalled = true;
                        cts.Cancel();
                        return;
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Normal shutdown. Pause(), completion, or the stall
                // branch above all end this loop the same way.
            }
            catch (Exception ex)
            {
                // The watchdog is fire-and-forget: anything but
                // cancellation escaping here would surface as an
                // unobserved task exception. Log it and let the download
                // continue without stall detection rather than faulting.
                System.Diagnostics.Debug.WriteLine($"Download watchdog fault: {ex.GetType().Name}");
            }
        });

        var outcome = DownloadOutcome.Failed;
        try
        {
            long existingBytes = File.Exists(destPath) ? new FileInfo(destPath).Length : 0;
            UpdateSnapshot(new DownloadProgress(progressKey, existingBytes, 0));

            var response = await SendGetAsync(sourceUrl, existingBytes, cts.Token);

            // A server honoring Range answers a start-point at/past the
            // resource's length with 416, not 200/206: exactly what
            // re-downloading an already-Completed file sends. Retry
            // without Range rather than let it fall through to a false
            // Failed.
            if (ShouldRetryWithoutRange(existingBytes, response.StatusCode))
            {
                response.Dispose();
                existingBytes = 0;
                response = await SendGetAsync(sourceUrl, 0, cts.Token);
            }

            using var _ = response;

            // Server may not support Range (206). If it ignores us and
            // sends 200 with the full body, restart the file from scratch
            // rather than corrupting it with a mismatched offset.
            (existingBytes, var resuming) = DetermineResumeStrategy(existingBytes, response.StatusCode);

            response.EnsureSuccessStatusCode();

            var totalBytes = (response.Content.Headers.ContentLength ?? 0) + existingBytes;

            await using var httpStream = await response.Content.ReadAsStreamAsync(cts.Token);
            await using var fileStream = new FileStream(
                destPath,
                resuming ? FileMode.Append : FileMode.Create,
                FileAccess.Write,
                FileShare.None);

            var buffer = new byte[81920];
            long totalRead = existingBytes;
            int read;
            var progressClock = System.Diagnostics.Stopwatch.StartNew();
            const long minReportIntervalMs = 100;

            while ((read = await httpStream.ReadAsync(buffer, cts.Token)) > 0)
            {
                await fileStream.WriteAsync(buffer.AsMemory(0, read), cts.Token);
                totalRead += read;
                lastProgressUtc = DateTime.UtcNow;

                if (progressClock.ElapsedMilliseconds >= minReportIntervalMs)
                {
                    UpdateSnapshot(new DownloadProgress(progressKey, totalRead, totalBytes));
                    progressClock.Restart();
                }
            }

            // Always report the final tally even if the last chunk landed
            // inside the throttle window, so the row doesn't visibly stall
            // short of 100% before flipping to Completed.
            UpdateSnapshot(new DownloadProgress(progressKey, totalRead, totalBytes));

            ApplyBackupExclusion(destPath);

            outcome = DownloadOutcome.Completed;
            return Path.Combine(subfolder, fileName);
        }
        catch (OperationCanceledException) when (stalled)
        {
            // Watchdog fired, not Pause(): surface as a distinct failure.
            // Partial bytes stay on disk; the existing 416/resume path
            // picks this back up on retry like any other
            // Failed artifact, no separate resume logic needed.
            throw new DownloadStalledException(
                $"No data received for {StallTimeout.TotalSeconds:0}s: connection appears stalled.");
        }
        catch (OperationCanceledException)
        {
            // Cancel() flags itself in _cancelledKeys before cancelling
            // the CTS, so an unflagged cancellation here is Pause() (or
            // the caller's own token), never Cancel().
            lock (_stateLock)
                outcome = _cancelledKeys.Remove(progressKey) ? DownloadOutcome.Cancelled : DownloadOutcome.Paused;
            if (outcome is DownloadOutcome.Cancelled && File.Exists(destPath))
            {
                // Backstop for a race Cancel() cannot close: it deletes
                // the partial, but this task can still open (recreating)
                // the file afterwards before observing the cancellation.
                // The file stream is disposed by now (await using unwinds
                // before the catch), so deleting here is safe.
                File.Delete(destPath);
            }
            throw;
        }
        finally
        {
            cts.Cancel(); // let the watchdog loop exit promptly if it hasn't already
            lock (_stateLock)
            {
                _activeDownloads.Remove(progressKey);
                _cancelledKeys.Remove(progressKey);
                if (outcome is DownloadOutcome.Paused)
                {
                    // A paused download keeps its last snapshot so the
                    // dashboard can keep showing it ("Paused, 340 of
                    // 900 MB, tap to resume") plus the partial path so a
                    // later Cancel() can still delete the partial file.
                    // The item-level method records the outcome for its
                    // own catch block below.
                    _lastOutcome[progressKey] = outcome;
                }
                else
                {
                    if (outcome is DownloadOutcome.Cancelled)
                        _lastOutcome[progressKey] = outcome;
                    else
                        _lastOutcome.Remove(progressKey);
                    _pendingPaths.Remove(progressKey);
                    _latestProgress.Remove(progressKey);
                }
            }
            FireAggregateChanged();
        }
    }

    /// <summary>
    /// Drives one artifact from NotStarted through InProgress to
    /// Completed (extracting zips on the way), Paused, or Failed,
    /// updating the entity's live progress on the main thread and
    /// persisting every terminal state. Cancel() mid-flight resets the
    /// entity to NotStarted and returns normally; a watchdog stall
    /// rethrows DownloadStalledException so the caller can message it.
    /// </summary>
    public async Task DownloadArtifactAsync(Artifact artifact)
    {
        // Guards a double-tap firing a second concurrent DownloadAsync for
        // the same row.
        if (artifact.DownloadStatus is DownloadStatus.InProgress or DownloadStatus.Extracting)
            return;

        var progressKey = $"artifact-{artifact.Id}";
        var fileName = Path.GetFileName(new Uri(artifact.SourceUrl).LocalPath);
        var destPath = Path.Combine(_appPaths.Root, artifact.CourseId, fileName);
        ClearLastOutcome(progressKey);

        artifact.DownloadStatus = DownloadStatus.InProgress;
        artifact.Progress = 0;

        void OnItemProgress(DownloadProgress progress)
        {
            if (progress.Key != progressKey)
                return;
            _mainThread.BeginInvokeOnMainThread(() =>
            {
                artifact.BytesDownloaded = progress.BytesReceived;
                artifact.Progress = progress.Fraction;
            });
        }
        ProgressChanged += OnItemProgress;

        var upserted = false;
        try
        {
            var relativePath = await DownloadAsync(
                artifact.SourceUrl,
                artifact.CourseId,
                fileName,
                progressKey);

            artifact.LocalFilePath = relativePath;
            artifact.Progress = 1;
            artifact.FileSizeBytes = new FileInfo(Path.Combine(_appPaths.Root, relativePath)).Length;

            // Auto-extract zip artifacts once the download lands, instead
            // of leaving extraction unwired.
            if (artifact.FileType == ArtifactFileType.Zip)
            {
                // Intermediate state only: upserted stays false so the
                // finally below still persists the final Completed state
                // with IsExtracted set either way.
                artifact.DownloadStatus = DownloadStatus.Extracting;
                await _db.UpsertArtifactAsync(artifact);

                try
                {
                    var zipFullPath = Path.Combine(_appPaths.Root, relativePath);
                    var extractSubfolder = Path.Combine(artifact.CourseId, $"_extracted_{artifact.Id}");
                    await ExtractZipAsync(zipFullPath, extractSubfolder);
                    artifact.IsExtracted = true;
                }
                catch
                {
                    // Download succeeded; only unpacking failed, so the
                    // status stays Completed, not Failed (re-downloading
                    // won't fix a bad archive or a full disk).
                    artifact.IsExtracted = false;
                    artifact.DownloadStatus = DownloadStatus.Completed;
                    return;
                }
            }

            artifact.DownloadStatus = DownloadStatus.Completed;
        }
        catch (DownloadStalledException)
        {
            artifact.DownloadStatus = DownloadStatus.Failed;
            await _db.UpsertArtifactAsync(artifact);
            upserted = true;
            throw;
        }
        catch (OperationCanceledException)
        {
            if (TakeLastOutcome(progressKey) is DownloadOutcome.Cancelled)
            {
                // Cancel(): the partial file is already gone (Cancel
                // deletes it; this is the no-op backstop), the row goes
                // back to NotStarted, and no exception escapes.
                if (File.Exists(destPath))
                    File.Delete(destPath);
                artifact.LocalFilePath = null;
                artifact.IsExtracted = false;
                artifact.Progress = 0;
                artifact.BytesDownloaded = 0;
                artifact.FileSizeBytes = 0;
                artifact.DownloadStatus = DownloadStatus.NotStarted;
            }
            else
            {
                // Pause(): partial bytes stay on disk for resume.
                artifact.DownloadStatus = DownloadStatus.Paused;
            }
            await _db.UpsertArtifactAsync(artifact);
            upserted = true;
            // The transport's AggregateChanged fired before this catch ran
            // (its finally runs first), so refresh once more now that the
            // entity's terminal state is persisted.
            FireAggregateChanged();
        }
        catch (Exception)
        {
            artifact.DownloadStatus = DownloadStatus.Failed;
            await _db.UpsertArtifactAsync(artifact);
            upserted = true;
            throw;
        }
        finally
        {
            ProgressChanged -= OnItemProgress;
            if (!upserted)
                await _db.UpsertArtifactAsync(artifact);
        }
    }

    /// <summary>
    /// Same orchestration as DownloadArtifactAsync, for lectures (no zip
    /// extraction step).
    /// </summary>
    public async Task DownloadLectureAsync(Lecture lecture)
    {
        if (lecture.DownloadStatus == DownloadStatus.InProgress)
            return;

        var progressKey = $"lecture-{lecture.Id}";
        var fileName = Path.GetFileName(new Uri(lecture.VideoUrl).LocalPath);
        var destPath = Path.Combine(_appPaths.Root, lecture.CourseId, fileName);
        ClearLastOutcome(progressKey);

        lecture.DownloadStatus = DownloadStatus.InProgress;
        lecture.Progress = 0;

        void OnItemProgress(DownloadProgress progress)
        {
            if (progress.Key != progressKey)
                return;
            _mainThread.BeginInvokeOnMainThread(() =>
            {
                lecture.BytesDownloaded = progress.BytesReceived;
                lecture.Progress = progress.Fraction;
            });
        }
        ProgressChanged += OnItemProgress;

        var upserted = false;
        try
        {
            var relativePath = await DownloadAsync(
                lecture.VideoUrl,
                lecture.CourseId,
                fileName,
                progressKey);

            lecture.LocalVideoPath = relativePath;
            lecture.Progress = 1;
            lecture.FileSizeBytes = new FileInfo(Path.Combine(_appPaths.Root, relativePath)).Length;
            lecture.DownloadStatus = DownloadStatus.Completed;
        }
        catch (DownloadStalledException)
        {
            lecture.DownloadStatus = DownloadStatus.Failed;
            await _db.UpsertLectureAsync(lecture);
            upserted = true;
            throw;
        }
        catch (OperationCanceledException)
        {
            if (TakeLastOutcome(progressKey) is DownloadOutcome.Cancelled)
            {
                if (File.Exists(destPath))
                    File.Delete(destPath);
                lecture.LocalVideoPath = null;
                lecture.Progress = 0;
                lecture.BytesDownloaded = 0;
                lecture.FileSizeBytes = 0;
                lecture.DownloadStatus = DownloadStatus.NotStarted;
            }
            else
            {
                lecture.DownloadStatus = DownloadStatus.Paused;
            }
            await _db.UpsertLectureAsync(lecture);
            upserted = true;
            FireAggregateChanged();
        }
        catch (Exception)
        {
            lecture.DownloadStatus = DownloadStatus.Failed;
            await _db.UpsertLectureAsync(lecture);
            upserted = true;
            throw;
        }
        finally
        {
            ProgressChanged -= OnItemProgress;
            if (!upserted)
                await _db.UpsertLectureAsync(lecture);
        }
    }

    public void Pause(string progressKey)
    {
        lock (_stateLock)
        {
            if (_activeDownloads.TryGetValue(progressKey, out var cts))
                cts.Cancel();
        }
    }

    /// <summary>
    /// Stops the transfer and deletes the partial file, so stranded
    /// partials cannot silently occupy storage. A paused download keeps
    /// its snapshot/partial path until resumed or cancelled, so Cancel
    /// also works on paused keys; a fully unknown key is a silent no-op.
    /// The in-flight item method sees the flag and resets its entity.
    /// </summary>
    public void Cancel(string progressKey)
    {
        CancellationTokenSource? cts;
        string? partialPath;
        lock (_stateLock)
        {
            _activeDownloads.TryGetValue(progressKey, out cts);
            _pendingPaths.TryGetValue(progressKey, out partialPath);
            if (cts is null && partialPath is null)
                return; // unknown key: silent no-op
            if (cts is not null)
            {
                _cancelledKeys.Add(progressKey);
                cts.Cancel();
            }
            _latestProgress.Remove(progressKey);
            _lastOutcome.Remove(progressKey);
        }

        if (partialPath is not null && File.Exists(partialPath))
            File.Delete(partialPath);

        FireAggregateChanged();
    }

    /// <summary>Latest per-key snapshots, for the dashboard's active section.</summary>
    public IReadOnlyList<ActiveDownload> GetActiveDownloads()
    {
        lock (_stateLock)
            return _latestProgress.Values
                .Select(p => new ActiveDownload(p.Key, p.BytesReceived, p.TotalBytes))
                .ToList();
    }

    public DownloadAggregate GetAggregate()
    {
        lock (_stateLock)
            return DownloadAggregate.Compute(_latestProgress.Values);
    }

    internal static bool ShouldRetryWithoutRange(long existingBytes, HttpStatusCode statusCode) =>
        existingBytes > 0 && statusCode == HttpStatusCode.RequestedRangeNotSatisfiable;

    internal static (long ExistingBytes, bool Resuming) DetermineResumeStrategy(long existingBytes, HttpStatusCode statusCode)
    {
        var resuming = existingBytes > 0 && statusCode == HttpStatusCode.PartialContent;
        return (resuming ? existingBytes : 0, resuming);
    }

    /// <summary>Issues the GET for DownloadAsync with a conditional Range header. Pulled out because the 416 handling above needs to send it twice.</summary>
    private async Task<HttpResponseMessage> SendGetAsync(string sourceUrl, long fromByte, CancellationToken token)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, sourceUrl);
        if (fromByte > 0)
            request.Headers.Range = new System.Net.Http.Headers.RangeHeaderValue(fromByte, null);

        return await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token);
    }

    private void UpdateSnapshot(DownloadProgress progress)
    {
        lock (_stateLock)
            _latestProgress[progress.Key] = progress;
        ProgressChanged?.Invoke(progress);
        FireAggregateChanged();
    }

    private void FireAggregateChanged()
    {
        DownloadAggregate aggregate;
        lock (_stateLock)
            aggregate = DownloadAggregate.Compute(_latestProgress.Values);
        AggregateChanged?.Invoke(aggregate);
    }

    private void ClearLastOutcome(string progressKey)
    {
        lock (_stateLock)
            _lastOutcome.Remove(progressKey);
    }

    private DownloadOutcome? TakeLastOutcome(string progressKey)
    {
        lock (_stateLock)
            return _lastOutcome.Remove(progressKey, out var outcome) ? outcome : null;
    }

    private enum DownloadOutcome
    {
        Completed,
        Paused,
        Cancelled,
        Failed
    }

    /// <summary>
    /// Sets NSURLIsExcludedFromBackupKey so downloaded course content
    /// doesn't bloat the user's iCloud backup, per the technical brief.
    /// No-op on non-iOS platforms.
    /// </summary>
    private static void ApplyBackupExclusion(string filePath)
    {
#if IOS
        var url = Foundation.NSUrl.FromFilename(filePath);
        url.SetResource(Foundation.NSUrlResourceKey.IsExcludedFromBackupKey, Foundation.NSNumber.FromBoolean(true));
#endif
        // Android's Auto Backup is opt-in-by-default and controlled from
        // AndroidManifest.xml (allowBackup="false"), not a per-file API
        // call like iOS.
    }

    /// <summary>Unzips a course archive into Documents/{destinationSubfolder}/. Runs off the UI thread: archives can be large enough to visibly stall extraction.</summary>
    public Task ExtractZipAsync(string zipPath, string destinationSubfolder)
    {
        var destDir = Path.Combine(_appPaths.Root, destinationSubfolder);
        Directory.CreateDirectory(destDir);
        return Task.Run(() =>
            System.IO.Compression.ZipFile.ExtractToDirectory(zipPath, destDir, overwriteFiles: true));
    }

    public async Task<long> GetTotalStorageUsedAsync()
    {
        var root = _appPaths.Root;
        if (!Directory.Exists(root)) return 0;

        return await Task.Run(() =>
            Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
                .Sum(f => new FileInfo(f).Length));
    }

    /// <summary>Actual on-disk bytes for one course's folder, for the dashboard's per-course breakdown.</summary>
    public async Task<long> GetStorageUsedByCourseAsync(string courseId)
    {
        var dir = Path.Combine(_appPaths.Root, courseId);
        if (!Directory.Exists(dir)) return 0;

        return await Task.Run(() =>
            Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories)
                .Sum(f => new FileInfo(f).Length));
    }

    /// <summary>Deletes a single downloaded file (artifact or lecture) from disk. Silently no-ops if it's already gone.</summary>
    public void DeleteFile(string relativePath)
    {
        var fullPath = Path.Combine(_appPaths.Root, relativePath);
        if (File.Exists(fullPath))
            File.Delete(fullPath);
    }

    /// <summary>Deletes an entire course's folder (downloads + any extracted zip contents).</summary>
    public void DeleteCourseFolder(string courseId)
    {
        var dir = Path.Combine(_appPaths.Root, courseId);
        if (Directory.Exists(dir))
            Directory.Delete(dir, recursive: true);
    }

    /// <summary>
    /// Deletes just the extracted-contents folder for one zip artifact
    /// (see ExtractZipAsync's per-artifact subfolder convention). Silently
    /// no-ops if nothing was ever extracted.
    /// </summary>
    public void DeleteExtractedContents(string courseId, int artifactId)
    {
        var dir = Path.Combine(_appPaths.Root, courseId, $"_extracted_{artifactId}");
        if (Directory.Exists(dir))
            Directory.Delete(dir, recursive: true);
    }
}
