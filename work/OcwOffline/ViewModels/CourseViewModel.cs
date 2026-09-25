using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OcwOffline.Models;
using OcwOffline.Services;

namespace OcwOffline.ViewModels;

/// <summary>
/// Drives the Find-a-course screen: first-run intro, Get course lookup,
/// and download rows with one stateful action button each. Download
/// orchestration lives in IDownloadManager; this class maps UI actions to
/// manager calls and surfaces fetch state for the page.
/// </summary>
public partial class CourseViewModel : ObservableObject
{
    private readonly IOcwScraperService _scraper;
    private readonly ICourseDatabase _db;
    private readonly IDownloadManager _downloads;
    private readonly ILastCourseStore _lastCourse;
    private readonly IConnectivityService _connectivity;

    private bool _restoreAttempted;

    public CourseViewModel(
        IOcwScraperService scraper,
        ICourseDatabase db,
        IDownloadManager downloads,
        ILastCourseStore lastCourse,
        IConnectivityService connectivity)
    {
        _scraper = scraper;
        _db = db;
        _downloads = downloads;
        _lastCourse = lastCourse;
        _connectivity = connectivity;
    }

    public event Action<Artifact>? OpenArtifactRequested;
    public event Action<Lecture>? OpenLectureRequested;
    public event Action? NavigateToDownloadsRequested;
    public event Action? RefocusCourseEntryRequested;

    [ObservableProperty]
    private string courseSlug = string.Empty;

    // Transient messages only (item download hiccups). Fetch failures go
    // to the error panel (HasFetchError and friends), never here.
    [ObservableProperty]
    private string statusText = string.Empty;

    [ObservableProperty]
    private bool isBusy;

    [ObservableProperty]
    private long totalStorageUsedBytes;

    [ObservableProperty]
    private bool showingLectures;

    // False until the first successful fetch: the tab toggles and lists
    // stay hidden behind the first-run intro until then.
    [ObservableProperty]
    private bool hasCompletedFetch;

    [ObservableProperty]
    private string fetchedCourseTitle = string.Empty;

    [ObservableProperty]
    private bool hasFetchError;

    [ObservableProperty]
    private string fetchErrorTitle = string.Empty;

    [ObservableProperty]
    private string fetchErrorDetail = string.Empty;

    [ObservableProperty]
    private bool isNetworkError;

    /// <summary>
    /// The first-run intro shows only before the first successful fetch
    /// and only when no fetch error is showing: after a failed first
    /// fetch the error panel replaces the intro instead of stacking
    /// beneath it.
    /// </summary>
    public bool IsFirstRunIntroVisible => !HasCompletedFetch && !HasFetchError;

    partial void OnHasCompletedFetchChanged(bool value) =>
        OnPropertyChanged(nameof(IsFirstRunIntroVisible));

    partial void OnHasFetchErrorChanged(bool value) =>
        OnPropertyChanged(nameof(IsFirstRunIntroVisible));

    public ObservableCollection<Artifact> Artifacts { get; } = new();
    public ObservableCollection<Lecture> Lectures { get; } = new();

    [RelayCommand]
    private void ShowTab(string tab)
    {
        ShowingLectures = tab == "Lectures";
    }

    [RelayCommand]
    private async Task GetCourseAsync()
    {
        if (string.IsNullOrWhiteSpace(CourseSlug) || IsBusy) return;

        IsBusy = true;
        HasFetchError = false;
        FetchErrorTitle = string.Empty;
        FetchErrorDetail = string.Empty;
        StatusText = "Looking up the course…";
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
            // Reconciles by SourceUrl/VideoUrl so re-fetch updates existing
            // rows in place instead of duplicating.
            if (!string.IsNullOrWhiteSpace(scraped.ZipArchiveUrl))
            {
                var courseZip = await _db.FindArtifactBySourceUrlAsync(scraped.CourseId, scraped.ZipArchiveUrl)
                    ?? new Artifact { CourseId = scraped.CourseId, SourceUrl = scraped.ZipArchiveUrl };

                courseZip.Title = "Download course (full zip)";
                courseZip.FileType = ArtifactFileType.Zip;

                await _db.UpsertArtifactAsync(courseZip);
                Artifacts.Add(courseZip);
            }

            foreach (var a in scraped.Artifacts)
            {
                if (string.Equals(a.SourceUrl, scraped.ZipArchiveUrl, StringComparison.OrdinalIgnoreCase))
                    continue; // already added above as the course zip

                var artifact = await _db.FindArtifactBySourceUrlAsync(scraped.CourseId, a.SourceUrl)
                    ?? new Artifact { CourseId = scraped.CourseId, SourceUrl = a.SourceUrl };

                artifact.Title = a.Title;
                artifact.FileType = a.FileType;
                artifact.FileSizeBytes = a.FileSizeBytes;

                await _db.UpsertArtifactAsync(artifact);
                Artifacts.Add(artifact);
            }

            foreach (var l in scraped.Lectures)
            {
                var lecture = await _db.FindLectureAsync(scraped.CourseId, l.VideoUrl, l.Title)
                    ?? new Lecture { CourseId = scraped.CourseId, VideoUrl = l.VideoUrl };

                lecture.Title = l.Title;
                lecture.FileSizeBytes = l.FileSizeBytes;

                await _db.UpsertLectureAsync(lecture);
                Lectures.Add(lecture);
            }

            FetchedCourseTitle = scraped.Title;
            HasCompletedFetch = true;
            _lastCourse.SetLastCourseId(scraped.CourseId);

            StatusText = scraped.UnmatchedResourceLabels.Count > 0
                ? $"{scraped.UnmatchedResourceLabels.Count} label(s) didn't match the expected format; skipped."
                : string.Empty;
        }
        catch (Exception ex)
        {
            // The raw exception never reaches the UI; the panel shows the
            // classified plain-language case instead.
            var info = FetchErrorInfo.ClassifyFetchError(ex);
            FetchErrorTitle = info.Title;
            FetchErrorDetail = info.Detail;
            IsNetworkError = info.IsNetworkError;
            HasFetchError = true;
            StatusText = string.Empty;
        }
        finally
        {
            IsBusy = false;
        }

        await RefreshStorageAsync();
    }

    /// <summary>
    /// Restores the last successfully fetched course on startup when there
    /// is connectivity. Runs once per ViewModel lifetime. Offline, or with
    /// nothing stored, the first-run intro stays put: an offline user must
    /// never land on an error screen.
    /// </summary>
    public async Task RestoreLastCourseAsync()
    {
        if (_restoreAttempted) return;
        _restoreAttempted = true;

        var courseId = _lastCourse.GetLastCourseId();
        if (string.IsNullOrWhiteSpace(courseId))
            return;
        if (!_connectivity.IsConnected)
            return;

        CourseSlug = courseId;
        await GetCourseAsync();
    }

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

    // The row's single primary button. One action per state, so the row
    // template needs no per-state buttons of its own.
    [RelayCommand]
    private async Task PrimaryArtifactActionAsync(Artifact artifact)
    {
        switch (artifact.DownloadStatus)
        {
            case DownloadStatus.NotStarted:
            case DownloadStatus.Failed:
                await DownloadArtifactAsync(artifact);
                break;
            case DownloadStatus.InProgress:
                _downloads.Pause($"artifact-{artifact.Id}");
                break;
            case DownloadStatus.Paused:
                // Resume re-runs the item download; the manager resumes
                // from the partial bytes it kept.
                await DownloadArtifactAsync(artifact);
                break;
            case DownloadStatus.Completed:
                OpenArtifactRequested?.Invoke(artifact);
                break;
        }
    }

    [RelayCommand]
    private async Task PrimaryLectureActionAsync(Lecture lecture)
    {
        switch (lecture.DownloadStatus)
        {
            case DownloadStatus.NotStarted:
            case DownloadStatus.Failed:
                await DownloadLectureAsync(lecture);
                break;
            case DownloadStatus.InProgress:
                _downloads.Pause($"lecture-{lecture.Id}");
                break;
            case DownloadStatus.Paused:
                await DownloadLectureAsync(lecture);
                break;
            case DownloadStatus.Completed:
                OpenLectureRequested?.Invoke(lecture);
                break;
        }
    }

    // Thin by design: the manager owns the download state machine. This
    // only guards re-entry, messages the stall case, and refreshes storage.
    private async Task DownloadArtifactAsync(Artifact artifact)
    {
        if (artifact.DownloadStatus is DownloadStatus.InProgress or DownloadStatus.Extracting)
            return;

        try
        {
            await _downloads.DownloadArtifactAsync(artifact);
        }
        catch (DownloadStalledException ex)
        {
            StatusText = $"\"{artifact.Title}\" stalled: {ex.Message}";
        }
        catch (OperationCanceledException)
        {
            // Pause path: the manager already moved the row to Paused, so
            // there is nothing to message here.
        }
        catch (Exception ex)
        {
            StatusText = $"\"{artifact.Title}\" failed: {ex.Message}";
        }
        finally
        {
            await RefreshStorageAsync();
        }
    }

    private async Task DownloadLectureAsync(Lecture lecture)
    {
        if (string.IsNullOrWhiteSpace(lecture.VideoUrl))
        {
            StatusText = $"\"{lecture.Title}\" has no direct video file (YouTube-only); skipping.";
            return;
        }

        if (lecture.DownloadStatus is DownloadStatus.InProgress or DownloadStatus.Extracting)
            return;

        try
        {
            await _downloads.DownloadLectureAsync(lecture);
        }
        catch (DownloadStalledException ex)
        {
            StatusText = $"\"{lecture.Title}\" stalled: {ex.Message}";
        }
        catch (OperationCanceledException)
        {
            // Pause path: the manager already moved the row to Paused.
        }
        catch (Exception ex)
        {
            StatusText = $"\"{lecture.Title}\" failed: {ex.Message}";
        }
        finally
        {
            await RefreshStorageAsync();
        }
    }

    [RelayCommand]
    private async Task CancelArtifactAsync(Artifact artifact)
    {
        if (artifact.DownloadStatus is not (DownloadStatus.InProgress or DownloadStatus.Paused))
            return;

        _downloads.Cancel($"artifact-{artifact.Id}");

        artifact.LocalFilePath = null;
        artifact.IsExtracted = false;
        artifact.Progress = 0;
        artifact.BytesDownloaded = 0;
        artifact.DownloadStatus = DownloadStatus.NotStarted;
        await _db.UpsertArtifactAsync(artifact);
        await RefreshStorageAsync();
    }

    [RelayCommand]
    private async Task CancelLectureAsync(Lecture lecture)
    {
        if (lecture.DownloadStatus is not (DownloadStatus.InProgress or DownloadStatus.Paused))
            return;

        _downloads.Cancel($"lecture-{lecture.Id}");

        lecture.LocalVideoPath = null;
        lecture.Progress = 0;
        lecture.BytesDownloaded = 0;
        lecture.DownloadStatus = DownloadStatus.NotStarted;
        await _db.UpsertLectureAsync(lecture);
        await RefreshStorageAsync();
    }

    // The error panel's single action. A network failure just retries the
    // fetch; an address failure puts the cursor back in the entry field.
    [RelayCommand]
    private async Task RetryFetchAsync()
    {
        if (IsNetworkError)
            await GetCourseAsync();
        else
            RefocusCourseEntryRequested?.Invoke();
    }

    [RelayCommand]
    private void OpenDownloads() => NavigateToDownloadsRequested?.Invoke();
}
