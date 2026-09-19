using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OcwOffline.Models;
using OcwOffline.Services;

namespace OcwOffline.ViewModels;

/// <summary>
/// Drives a single-course screen: enter a slug, scrape it, download
/// what's picked. Deliberately minimal. See AUDIT_TRAIL v1.
/// </summary>
public partial class CourseViewModel : ObservableObject
{
    private readonly IOcwScraperService _scraper;
    private readonly ICourseDatabase _db;
    private readonly IDownloadManager _downloads;
    private readonly IMainThreadDispatcher _mainThread;
    private readonly IAppPaths _appPaths;

    public CourseViewModel(
        IOcwScraperService scraper,
        ICourseDatabase db,
        IDownloadManager downloads,
        IMainThreadDispatcher mainThread,
        IAppPaths appPaths)
    {
        _scraper = scraper;
        _db = db;
        _downloads = downloads;
        _mainThread = mainThread;
        _appPaths = appPaths;
        _downloads.ProgressChanged += OnProgressChanged;
    }

    [ObservableProperty]
    private string courseSlug = "hst-508-genomics-and-computational-biology-fall-2002";

    [ObservableProperty]
    private string statusText = "Enter a course slug (from its OCW URL) and tap Fetch.";

    [ObservableProperty]
    private bool isBusy;

    [ObservableProperty]
    private long totalStorageUsedBytes;

    public ObservableCollection<Artifact> Artifacts { get; } = new();
    public ObservableCollection<Lecture> Lectures { get; } = new();

    [RelayCommand]
    private async Task FetchAsync()
    {
        if (string.IsNullOrWhiteSpace(CourseSlug) || IsBusy) return;

        IsBusy = true;
        StatusText = "Scraping course page...";
        Artifacts.Clear();
        Lectures.Clear();

        try
        {
            var scraped = await _scraper.ScrapeDownloadPageAsync(CourseSlug.Trim());

            await _db.UpsertCourseAsync(new Course
            {
                Id = scraped.CourseId,
                Title = scraped.Title,
                Url = scraped.SourceUrl,
                LastScrapedUtc = DateTime.UtcNow
            });

            // The course zip is surfaced first as a downloadable Artifact.

            // Reconciles by SourceUrl/VideoUrl so re-Fetch updates existing
            // rows in place instead of duplicating.
            if (!string.IsNullOrWhiteSpace(scraped.ZipArchiveUrl))
            {
                var courseZip = ResolveLiveArtifact(await _db.FindArtifactBySourceUrlAsync(scraped.CourseId, scraped.ZipArchiveUrl)
                    ?? new Artifact { CourseId = scraped.CourseId, SourceUrl = scraped.ZipArchiveUrl });

                courseZip.Title = "Download course (full zip)";
                courseZip.FileType = ArtifactFileType.Zip;

                await _db.UpsertArtifactAsync(courseZip);
                Artifacts.Add(courseZip);
            }

            foreach (var a in scraped.Artifacts)
            {
                if (string.Equals(a.SourceUrl, scraped.ZipArchiveUrl, StringComparison.OrdinalIgnoreCase))
                    continue; // already added above as the course zip

                var artifact = ResolveLiveArtifact(await _db.FindArtifactBySourceUrlAsync(scraped.CourseId, a.SourceUrl)
                    ?? new Artifact { CourseId = scraped.CourseId, SourceUrl = a.SourceUrl });

                artifact.Title = a.Title;
                artifact.FileType = a.FileType;
                artifact.FileSizeBytes = a.FileSizeBytes;

                await _db.UpsertArtifactAsync(artifact);
                Artifacts.Add(artifact);
            }

            foreach (var l in scraped.Lectures)
            {
                var lecture = ResolveLiveLecture(await _db.FindLectureAsync(scraped.CourseId, l.VideoUrl, l.Title)
                    ?? new Lecture { CourseId = scraped.CourseId, VideoUrl = l.VideoUrl });

                lecture.Title = l.Title;
                lecture.FileSizeBytes = l.FileSizeBytes;

                await _db.UpsertLectureAsync(lecture);
                Lectures.Add(lecture);
            }

            StatusText = $"Found {Artifacts.Count} resource(s) and {Lectures.Count} lecture video(s).";
            if (scraped.UnmatchedResourceLabels.Count > 0)
                StatusText += $" {scraped.UnmatchedResourceLabels.Count} label(s) didn't match the expected format; skipped.";
        }
        catch (Exception ex)
        {
            StatusText = $"Failed: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }

        await RefreshStorageAsync();
    }

    // Item 5 from the handoff: GetTotalStorageUsedAsync existed but
    // nothing surfaced it. Called after fetch/download/delete so the
    // dashboard number stays current without a manual refresh button.
    [RelayCommand]
    private async Task RefreshStorageAsync()
    {
        TotalStorageUsedBytes = await _downloads.GetTotalStorageUsedAsync();
    }

    [RelayCommand]
    private async Task DeleteArtifactAsync(Artifact artifact)
    {
        // Guards Delete racing an in-flight download for the same row
        // (file deleted out from under an open write handle, then
        // clobbered back to Completed). See AUDIT_TRAIL v7.
        if (artifact.DownloadStatus is DownloadStatus.InProgress or DownloadStatus.Extracting)
            return;

        if (!string.IsNullOrEmpty(artifact.LocalFilePath))
            _downloads.DeleteFile(artifact.LocalFilePath);

        if (artifact.IsExtracted)
            _downloads.DeleteExtractedContents(artifact.CourseId, artifact.Id);

        artifact.LocalFilePath = null;
        artifact.IsExtracted = false;
        artifact.Progress = 0;
        artifact.BytesDownloaded = 0;
        artifact.DownloadStatus = DownloadStatus.NotStarted;
        await _db.UpsertArtifactAsync(artifact);
        await RefreshStorageAsync();
    }

    [RelayCommand]
    private async Task DeleteLectureAsync(Lecture lecture)
    {
        // See the matching guard in DeleteArtifactAsync: the same
        // Delete-vs-in-flight-download race applies here (lectures never
        // enter Extracting, so InProgress is the only state to check).
        if (lecture.DownloadStatus == DownloadStatus.InProgress)
            return;

        if (!string.IsNullOrEmpty(lecture.LocalVideoPath))
            _downloads.DeleteFile(lecture.LocalVideoPath);

        lecture.LocalVideoPath = null;
        lecture.Progress = 0;
        lecture.BytesDownloaded = 0;
        lecture.DownloadStatus = DownloadStatus.NotStarted;
        await _db.UpsertLectureAsync(lecture);
        await RefreshStorageAsync();
    }

    // Wires the Pause button. No-ops outside InProgress.
    [RelayCommand]
    private void PauseArtifact(Artifact artifact)
    {
        if (artifact.DownloadStatus != DownloadStatus.InProgress) return;
        _downloads.Pause($"artifact-{artifact.Id}");
    }

    [RelayCommand]
    private void PauseLecture(Lecture lecture)
    {
        if (lecture.DownloadStatus != DownloadStatus.InProgress) return;
        _downloads.Pause($"lecture-{lecture.Id}");
    }

    [RelayCommand]
    private async Task DownloadArtifactAsync(Artifact artifact)
    {
        // Guards a double-tap firing a second concurrent DownloadAsync for
        // the same row (XAML doesn't disable this button mid-download).
        // Same pattern as FetchAsync's IsBusy guard.
        if (artifact.DownloadStatus is DownloadStatus.InProgress or DownloadStatus.Extracting)
            return;

        artifact.DownloadStatus = DownloadStatus.InProgress;
        artifact.Progress = 0;
        var progressKey = $"artifact-{artifact.Id}";
        _progressTargets[progressKey] = artifact;

        try
        {
            var relativePath = await _downloads.DownloadAsync(
                artifact.SourceUrl,
                subfolder: artifact.CourseId,
                fileName: Path.GetFileName(new Uri(artifact.SourceUrl).LocalPath),
                progressKey: progressKey);

            artifact.LocalFilePath = relativePath;
            artifact.Progress = 1;

            // Item 1 from the handoff: auto-extract zip artifacts once the
            // download lands, instead of leaving ExtractZip unwired.
            if (artifact.FileType == ArtifactFileType.Zip)
            {
                artifact.DownloadStatus = DownloadStatus.Extracting;
                await _db.UpsertArtifactAsync(artifact);

                try
                {
                    var zipFullPath = Path.Combine(_appPaths.Root, relativePath);
                    var extractSubfolder = Path.Combine(artifact.CourseId, $"_extracted_{artifact.Id}");
                    await _downloads.ExtractZipAsync(zipFullPath, destinationSubfolder: extractSubfolder);
                    artifact.IsExtracted = true;
                }
                catch
                {
                    // Download succeeded; only unpacking failed, so the status
                    // stays Completed, not Failed (re-downloading won't fix
                    // a bad archive or a full disk).
                    artifact.IsExtracted = false;
                    artifact.DownloadStatus = DownloadStatus.Completed;
                    return;
                }
            }

            artifact.DownloadStatus = DownloadStatus.Completed;
        }
        catch (OperationCanceledException)
        {
            // User-initiated Pause(). DownloadManager.DownloadStalledException
            // (below) is how a watchdog-triggered cancellation stays distinct
            // from this.
            artifact.DownloadStatus = DownloadStatus.Paused;
        }
        catch (Exception ex)
        {
            artifact.DownloadStatus = DownloadStatus.Failed;
            StatusText = $"\"{artifact.Title}\" failed: {ex.Message}";
        }
        finally
        {
            _progressTargets.Remove(progressKey);
            await _db.UpsertArtifactAsync(artifact);
            await RefreshStorageAsync();
        }
    }

    [RelayCommand]
    private async Task DownloadLectureAsync(Lecture lecture)
    {
        if (string.IsNullOrWhiteSpace(lecture.VideoUrl))
        {
            StatusText = $"\"{lecture.Title}\" has no direct video file (YouTube-only); skipping.";
            return;
        }

        // See the matching guard in DownloadArtifactAsync: the same
        // double-tap/concurrent-download hazard applies here.
        if (lecture.DownloadStatus == DownloadStatus.InProgress)
            return;

        lecture.DownloadStatus = DownloadStatus.InProgress;
        lecture.Progress = 0;
        var progressKey = $"lecture-{lecture.Id}";
        _progressTargets[progressKey] = lecture;

        try
        {
            var relativePath = await _downloads.DownloadAsync(
                lecture.VideoUrl,
                subfolder: lecture.CourseId,
                fileName: Path.GetFileName(new Uri(lecture.VideoUrl).LocalPath),
                progressKey: progressKey);

            lecture.LocalVideoPath = relativePath;
            lecture.Progress = 1;
            lecture.DownloadStatus = DownloadStatus.Completed;
        }
        catch (OperationCanceledException)
        {
            // See the matching catch in DownloadArtifactAsync.
            lecture.DownloadStatus = DownloadStatus.Paused;
        }
        catch (Exception ex)
        {
            lecture.DownloadStatus = DownloadStatus.Failed;
            StatusText = $"\"{lecture.Title}\" failed: {ex.Message}";
        }
        finally
        {
            _progressTargets.Remove(progressKey);
            await _db.UpsertLectureAsync(lecture);
            await RefreshStorageAsync();
        }
    }

    // sqlite-net-pcl returns a fresh instance per query, orphaning any
    // in-flight download's live object. Prefers the _progressTargets
    // instance when one exists, refreshing only metadata. See AUDIT_TRAIL v10.
    private Artifact ResolveLiveArtifact(Artifact fetched) =>
        fetched.Id != 0 && _progressTargets.TryGetValue($"artifact-{fetched.Id}", out var live) && live is Artifact liveArtifact
            ? liveArtifact
            : fetched;

    private Lecture ResolveLiveLecture(Lecture fetched) =>
        fetched.Id != 0 && _progressTargets.TryGetValue($"lecture-{fetched.Id}", out var live) && live is Lecture liveLecture
            ? liveLecture
            : fetched;

    // Item 2 from the handoff: this was a no-op stub. Now maps a
    // DownloadManager progress event (keyed by "artifact-{id}" /
    // "lecture-{id}") back to whichever row is currently downloading,
    // so the XAML's per-row ProgressBar has something to bind to.
    private readonly Dictionary<string, object> _progressTargets = new();

    private void OnProgressChanged(DownloadProgress progress)
    {
        if (!_progressTargets.TryGetValue(progress.Key, out var target))
            return;

        // ProgressChanged fires from the download's read loop, which is
        // already on a background thread here (no Task.Run/ConfigureAwait
        // gymnastics in DownloadManager). MAUI bindings marshal to the
        // UI thread automatically on property-changed, but to be safe
        // across both platforms we hop back explicitly. Dispatched through
        // IMainThreadDispatcher (not the static MainThread) so this stays
        // testable on a bare net10.0 host. See IMainThreadDispatcher.
        _mainThread.BeginInvokeOnMainThread(() =>
        {
            switch (target)
            {
                case Artifact artifact:
                    artifact.BytesDownloaded = progress.BytesReceived;
                    artifact.Progress = progress.Fraction;
                    break;
                case Lecture lecture:
                    lecture.BytesDownloaded = progress.BytesReceived;
                    lecture.Progress = progress.Fraction;
                    break;
            }
        });
    }
}
