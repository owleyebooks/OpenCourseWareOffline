using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OcwOffline.Models;
using OcwOffline.Services;
using OcwOffline.Views;

namespace OcwOffline.ViewModels;

/// <summary>
/// One dashboard row: a course, its live on-disk size, and its downloaded
/// items (expandable). Not persisted; rebuilt each load from Course rows
/// plus a folder scan, so it cannot drift from disk.
/// </summary>
public partial class CourseStorageInfo : ObservableObject
{
    public string CourseId { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;

    [ObservableProperty]
    private long bytesUsed;

    [ObservableProperty]
    private bool isExpanded;

    public ObservableCollection<DownloadedItemInfo> Items { get; } = new();

    [RelayCommand]
    private void ToggleExpanded() => IsExpanded = !IsExpanded;
}

/// <summary>
/// One downloaded file inside an expanded course row. Carries its entity
/// so Open/Delete can act on it without another DB round-trip.
/// </summary>
public partial class DownloadedItemInfo : ObservableObject
{
    private readonly DownloadsDashboardViewModel _owner;

    public DownloadedItemInfo(DownloadsDashboardViewModel owner, Artifact artifact, long sizeBytes)
    {
        _owner = owner;
        Artifact = artifact;
        SizeText = ByteSizeConverter.Format(sizeBytes);
    }

    public DownloadedItemInfo(DownloadsDashboardViewModel owner, Lecture lecture, long sizeBytes)
    {
        _owner = owner;
        Lecture = lecture;
        SizeText = ByteSizeConverter.Format(sizeBytes);
    }

    public Artifact? Artifact { get; }
    public Lecture? Lecture { get; }

    public string Kind => Artifact is not null ? "Document" : "Video";
    public string Title => Artifact?.Title ?? Lecture?.Title ?? string.Empty;
    public string SizeText { get; }
    public string OpenLabel => Artifact is not null ? "Open" : "Watch";

    [RelayCommand]
    private void Open() => _owner.OpenItem(this);

    [RelayCommand]
    private Task DeleteAsync() => _owner.DeleteItemAsync(this);
}

/// <summary>
/// One in-progress or paused download at the top of the dashboard. The
/// entity is joined from the manager's progress key so the row has a
/// title; progress snapshots flow in from AggregateChanged.
/// </summary>
public partial class ActiveDownloadRow : ObservableObject
{
    private readonly DownloadsDashboardViewModel _owner;

    public ActiveDownloadRow(
        DownloadsDashboardViewModel owner,
        string progressKey,
        Artifact? artifact,
        Lecture? lecture)
    {
        _owner = owner;
        ProgressKey = progressKey;
        Artifact = artifact;
        Lecture = lecture;
    }

    public string ProgressKey { get; }
    public Artifact? Artifact { get; }
    public Lecture? Lecture { get; }

    public string Title => Artifact?.Title ?? Lecture?.Title ?? ProgressKey;

    public bool IsPaused =>
        (Artifact?.DownloadStatus ?? Lecture?.DownloadStatus) == DownloadStatus.Paused;

    [ObservableProperty]
    private double progress;

    [ObservableProperty]
    private string detailText = string.Empty;

    [RelayCommand]
    private void Pause() => _owner.PauseDownload(ProgressKey);

    [RelayCommand]
    private Task ResumeAsync() => _owner.ResumeDownloadAsync(this);

    [RelayCommand]
    private Task CancelAsync() => _owner.CancelDownloadAsync(this);
}

/// <summary>
/// Backend for the Downloads Dashboard: in-progress/paused downloads up
/// top (live via the manager's AggregateChanged), downloaded courses
/// below with per-item Open/Watch and Delete. Raises open events for the
/// page to push the viewer/player onto.
/// </summary>
public partial class DownloadsDashboardViewModel : ObservableObject
{
    private readonly ICourseDatabase _db;
    private readonly IDownloadManager _downloads;
    private readonly IMainThreadDispatcher _mainThread;
    private readonly IAppPaths _appPaths;

    public DownloadsDashboardViewModel(
        ICourseDatabase db,
        IDownloadManager downloads,
        IMainThreadDispatcher mainThread,
        IAppPaths appPaths)
    {
        _db = db;
        _downloads = downloads;
        _mainThread = mainThread;
        _appPaths = appPaths;
        _downloads.AggregateChanged += OnAggregateChanged;
    }

    public event Action<Artifact>? OpenArtifactRequested;
    public event Action<Lecture>? OpenLectureRequested;

    [ObservableProperty]
    private bool isBusy;

    [ObservableProperty]
    private string storageSummary = string.Empty;

    public ObservableCollection<ActiveDownloadRow> ActiveDownloads { get; } = new();
    public ObservableCollection<CourseStorageInfo> Courses { get; } = new();

    [RelayCommand]
    private async Task LoadAsync()
    {
        if (IsBusy) return;
        IsBusy = true;
        Courses.Clear();

        try
        {
            await RefreshActiveDownloadsAsync();

            foreach (var course in await _db.GetAllCoursesAsync())
            {
                var bytesUsed = await _downloads.GetStorageUsedByCourseAsync(course.Id);

                // A course can be scraped (a Course row exists) without
                // anything ever having been downloaded from it. Nothing on
                // disk means nothing for a *downloads* dashboard to show or
                // for its bulk-delete to usefully act on.
                if (bytesUsed == 0) continue;

                var info = new CourseStorageInfo
                {
                    CourseId = course.Id,
                    Title = string.IsNullOrWhiteSpace(course.Title) ? course.Id : course.Title,
                    BytesUsed = bytesUsed
                };
                await PopulateItemsAsync(info);
                Courses.Add(info);
            }

            UpdateStorageSummary();
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task DeleteCourseAsync(CourseStorageInfo course)
    {
        // Deletes both halves: files on disk (DownloadManager's job) and
        // the DB rows tracking them (CourseDatabase's job).
        _downloads.DeleteCourseFolder(course.CourseId);
        await _db.DeleteCourseDataAsync(course.CourseId);

        Courses.Remove(course);
        UpdateStorageSummary();
    }

    internal void OpenItem(DownloadedItemInfo item)
    {
        if (item.Artifact is not null)
            OpenArtifactRequested?.Invoke(item.Artifact);
        else if (item.Lecture is not null)
            OpenLectureRequested?.Invoke(item.Lecture);
    }

    internal async Task DeleteItemAsync(DownloadedItemInfo item)
    {
        // Cancel first so a paused snapshot for this key can't linger in
        // the active section after its entity is gone. No-op when nothing
        // is in flight for the key.
        var progressKey = item.Artifact is not null
            ? $"artifact-{item.Artifact.Id}"
            : $"lecture-{item.Lecture?.Id}";
        _downloads.Cancel(progressKey);

        if (item.Artifact is not null)
        {
            var artifact = item.Artifact;
            if (!string.IsNullOrEmpty(artifact.LocalFilePath))
                _downloads.DeleteFile(artifact.LocalFilePath);
            if (artifact.IsExtracted)
                _downloads.DeleteExtractedContents(artifact.CourseId, artifact.Id);
            await _db.DeleteArtifactAsync(artifact.Id);
        }
        else if (item.Lecture is not null)
        {
            var lecture = item.Lecture;
            if (!string.IsNullOrEmpty(lecture.LocalVideoPath))
                _downloads.DeleteFile(lecture.LocalVideoPath);
            await _db.DeleteLectureAsync(lecture.Id);
        }

        var course = Courses.FirstOrDefault(c => c.Items.Contains(item));
        if (course is null)
            return;

        course.Items.Remove(item);
        course.BytesUsed = await _downloads.GetStorageUsedByCourseAsync(course.CourseId);
        if (course.Items.Count == 0)
            Courses.Remove(course);
        UpdateStorageSummary();
    }

    internal void PauseDownload(string progressKey) => _downloads.Pause(progressKey);

    internal async Task ResumeDownloadAsync(ActiveDownloadRow row)
    {
        try
        {
            if (row.Artifact is not null)
                await _downloads.DownloadArtifactAsync(row.Artifact);
            else if (row.Lecture is not null)
                await _downloads.DownloadLectureAsync(row.Lecture);
        }
        catch (Exception ex)
        {
            // The manager already persisted the Failed state; there is no
            // error surface on this dashboard, so log with context rather
            // than crash an async-void command invocation. A re-download
            // can be started from the Browse screen.
            System.Diagnostics.Debug.WriteLine(
                $"Dashboard resume of '{row.ProgressKey}' failed: {ex.GetType().Name}: {ex.Message}");
        }
    }

    internal async Task CancelDownloadAsync(ActiveDownloadRow row)
    {
        _downloads.Cancel(row.ProgressKey);

        // The in-flight item method (when the download was started from
        // this dashboard) resets the entity itself on the flagged
        // cancellation; this covers downloads started elsewhere and the
        // already-paused case. Both resets write the same values.
        if (row.Artifact is not null)
        {
            ResetArtifact(row.Artifact);
            await _db.UpsertArtifactAsync(row.Artifact);
        }
        else if (row.Lecture is not null)
        {
            ResetLecture(row.Lecture);
            await _db.UpsertLectureAsync(row.Lecture);
        }

        await RefreshActiveDownloadsAsync();
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

    private void OnAggregateChanged(DownloadAggregate aggregate) =>
        _mainThread.BeginInvokeOnMainThread(() => _ = RefreshActiveSectionAsync());

    // The live-refresh path must never fault the dispatcher callback: a
    // failed tick just skips this refresh and the next tick retries.
    // LoadAsync (page appearing) stays the authoritative load.
    private async Task RefreshActiveSectionAsync()
    {
        try
        {
            await RefreshActiveDownloadsAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Dashboard active-section refresh failed: {ex.GetType().Name}");
        }
    }

    private async Task RefreshActiveDownloadsAsync()
    {
        var rows = new List<ActiveDownloadRow>();
        foreach (var active in _downloads.GetActiveDownloads())
        {
            var (artifact, lecture) = await ResolveEntityAsync(active.Key);
            if (artifact is null && lecture is null)
                continue;

            rows.Add(new ActiveDownloadRow(this, active.Key, artifact, lecture)
            {
                Progress = active.Fraction,
                DetailText = FormatDetail(active)
            });
        }

        ActiveDownloads.Clear();
        foreach (var row in rows)
            ActiveDownloads.Add(row);
    }

    private static string FormatDetail(ActiveDownload active)
    {
        var percent = (int)Math.Round(active.Fraction * 100);
        return $"{percent}%, {ByteSizeConverter.Format(active.BytesReceived)} of {ByteSizeConverter.Format(active.TotalBytes)}";
    }

    private async Task<(Artifact? Artifact, Lecture? Lecture)> ResolveEntityAsync(string progressKey)
    {
        var dash = progressKey.LastIndexOf('-');
        if (dash < 0 || !int.TryParse(progressKey[(dash + 1)..], out var id))
            return (null, null);

        if (progressKey.StartsWith("artifact-", StringComparison.Ordinal))
            return (await _db.GetArtifactAsync(id), null);
        if (progressKey.StartsWith("lecture-", StringComparison.Ordinal))
            return (null, await _db.GetLectureAsync(id));
        return (null, null);
    }

    private async Task PopulateItemsAsync(CourseStorageInfo info)
    {
        info.Items.Clear();

        foreach (var artifact in await _db.GetArtifactsForCourseAsync(info.CourseId))
        {
            if (string.IsNullOrEmpty(artifact.LocalFilePath))
                continue;
            info.Items.Add(new DownloadedItemInfo(
                this, artifact, ResolveSizeBytes(artifact.FileSizeBytes, artifact.LocalFilePath)));
        }

        foreach (var lecture in await _db.GetLecturesForCourseAsync(info.CourseId))
        {
            if (string.IsNullOrEmpty(lecture.LocalVideoPath))
                continue;
            info.Items.Add(new DownloadedItemInfo(
                this, lecture, ResolveSizeBytes(lecture.FileSizeBytes, lecture.LocalVideoPath)));
        }
    }

    private long ResolveSizeBytes(long fileSizeBytes, string relativePath)
    {
        if (fileSizeBytes > 0)
            return fileSizeBytes;

        var fullPath = Path.Combine(_appPaths.Root, relativePath);
        return File.Exists(fullPath) ? new FileInfo(fullPath).Length : 0;
    }

    private void UpdateStorageSummary()
    {
        var total = Courses.Sum(c => c.BytesUsed);
        var count = Courses.Count;
        StorageSummary = $"Using {ByteSizeConverter.Format(total)} across {count} {(count == 1 ? "course" : "courses")}";
    }
}
