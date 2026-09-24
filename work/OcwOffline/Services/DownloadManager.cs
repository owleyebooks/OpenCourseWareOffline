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
/// Full reasoning: AUDIT_TRAIL v1.
/// </summary>
public class DownloadManager : IDownloadManager
{
    // HttpClient's default 100s timeout fires mid-download on large videos
    // and gets mislabeled Paused by the caller's cancellation-catch.
    // Disabled in favor of this class's own cancellation. See AUDIT_TRAIL v5.
    private readonly HttpClient _http = new() { Timeout = Timeout.InfiniteTimeSpan };
    private readonly Dictionary<string, CancellationTokenSource> _activeDownloads = new();

    // Replaces the disabled HttpClient timeout (see AUDIT_TRAIL v5) with a
    // narrower check: no bytes at all for StallTimeout, not a ceiling on
    // total download time. A slow-but-flowing download never trips this.
    // Backlog item resolved: see HANDOFF v19 §2. Full reasoning: AUDIT_TRAIL v19.
    private static readonly TimeSpan StallTimeout = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan StallCheckInterval = TimeSpan.FromSeconds(5);

    public event Action<DownloadProgress>? ProgressChanged;

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
        var dir = Path.Combine(AppPaths.Root, subfolder);
        Directory.CreateDirectory(dir);
        var destPath = Path.Combine(dir, fileName);

        var cts = CancellationTokenSource.CreateLinkedTokenSource(externalToken);
        _activeDownloads[progressKey] = cts;

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

        try
        {
            long existingBytes = File.Exists(destPath) ? new FileInfo(destPath).Length : 0;

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
                    ProgressChanged?.Invoke(new DownloadProgress(progressKey, totalRead, totalBytes));
                    progressClock.Restart();
                }
            }

            // Always report the final tally even if the last chunk landed
            // inside the throttle window, so the row doesn't visibly stall
            // short of 100% before flipping to Completed.
            ProgressChanged?.Invoke(new DownloadProgress(progressKey, totalRead, totalBytes));

            ApplyBackupExclusion(destPath);

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
        finally
        {
            cts.Cancel(); // let the watchdog loop exit promptly if it hasn't already
            _activeDownloads.Remove(progressKey);
        }
    }

    internal static bool ShouldRetryWithoutRange(long existingBytes, System.Net.HttpStatusCode statusCode) =>
        existingBytes > 0 && statusCode == System.Net.HttpStatusCode.RequestedRangeNotSatisfiable;

    internal static (long ExistingBytes, bool Resuming) DetermineResumeStrategy(long existingBytes, System.Net.HttpStatusCode statusCode)
    {
        var resuming = existingBytes > 0 && statusCode == System.Net.HttpStatusCode.PartialContent;
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

    public void Pause(string progressKey)
    {
        if (_activeDownloads.TryGetValue(progressKey, out var cts))
            cts.Cancel();
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
        var destDir = Path.Combine(AppPaths.Root, destinationSubfolder);
        Directory.CreateDirectory(destDir);
        return Task.Run(() =>
            System.IO.Compression.ZipFile.ExtractToDirectory(zipPath, destDir, overwriteFiles: true));
    }

    public async Task<long> GetTotalStorageUsedAsync()
    {
        var root = AppPaths.Root;
        if (!Directory.Exists(root)) return 0;

        return await Task.Run(() =>
            Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
                .Sum(f => new FileInfo(f).Length));
    }

    /// <summary>Actual on-disk bytes for one course's folder, for the dashboard's per-course breakdown.</summary>
    public async Task<long> GetStorageUsedByCourseAsync(string courseId)
    {
        var dir = Path.Combine(AppPaths.Root, courseId);
        if (!Directory.Exists(dir)) return 0;

        return await Task.Run(() =>
            Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories)
                .Sum(f => new FileInfo(f).Length));
    }

    /// <summary>Deletes a single downloaded file (artifact or lecture) from disk. Silently no-ops if it's already gone.</summary>
    public void DeleteFile(string relativePath)
    {
        var fullPath = Path.Combine(AppPaths.Root, relativePath);
        if (File.Exists(fullPath))
            File.Delete(fullPath);
    }

    /// <summary>Deletes an entire course's folder (downloads + any extracted zip contents).</summary>
    public void DeleteCourseFolder(string courseId)
    {
        var dir = Path.Combine(AppPaths.Root, courseId);
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
        var dir = Path.Combine(AppPaths.Root, courseId, $"_extracted_{artifactId}");
        if (Directory.Exists(dir))
            Directory.Delete(dir, recursive: true);
    }
}
