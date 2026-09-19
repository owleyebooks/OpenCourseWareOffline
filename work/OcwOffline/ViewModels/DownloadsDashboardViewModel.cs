using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OcwOffline.Services;

namespace OcwOffline.ViewModels;

/// <summary>
/// One dashboard row: a course plus its live on-disk size. Not persisted;
/// rebuilt each load from Course rows plus a folder scan, so it cannot drift
/// from disk.
/// </summary>
public partial class CourseStorageInfo : ObservableObject
{
    public string CourseId { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;

    [ObservableProperty]
    private long bytesUsed;
}

/// <summary>
/// Backend for the Downloads Dashboard (per-course breakdown, bulk
/// delete). Deliberately minimal, matching CourseViewModel's own scope.
/// See AUDIT_TRAIL v12.
/// </summary>
public partial class DownloadsDashboardViewModel : ObservableObject
{
    private readonly ICourseDatabase _db;
    private readonly IDownloadManager _downloads;

    public DownloadsDashboardViewModel(ICourseDatabase db, IDownloadManager downloads)
    {
        _db = db;
        _downloads = downloads;
    }

    [ObservableProperty]
    private bool isBusy;

    [ObservableProperty]
    private long totalStorageUsedBytes;

    public ObservableCollection<CourseStorageInfo> Courses { get; } = new();

    [RelayCommand]
    private async Task LoadAsync()
    {
        if (IsBusy) return;
        IsBusy = true;
        Courses.Clear();

        try
        {
            foreach (var course in await _db.GetAllCoursesAsync())
            {
                var bytesUsed = await _downloads.GetStorageUsedByCourseAsync(course.Id);

                // A course can be scraped (a Course row exists) without
                // anything ever having been downloaded from it. Nothing on
                // disk means nothing for a *downloads* dashboard to show or
                // for its bulk-delete to usefully act on.
                if (bytesUsed == 0) continue;

                Courses.Add(new CourseStorageInfo
                {
                    CourseId = course.Id,
                    Title = string.IsNullOrWhiteSpace(course.Title) ? course.Id : course.Title,
                    BytesUsed = bytesUsed
                });
            }

            TotalStorageUsedBytes = Courses.Sum(c => c.BytesUsed);
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
        // the DB rows tracking them (CourseDatabase's job; see
        // DeleteCourseDataAsync's own comment for why both are needed).
        _downloads.DeleteCourseFolder(course.CourseId);
        await _db.DeleteCourseDataAsync(course.CourseId);

        Courses.Remove(course);
        TotalStorageUsedBytes = Courses.Sum(c => c.BytesUsed);
    }
}
