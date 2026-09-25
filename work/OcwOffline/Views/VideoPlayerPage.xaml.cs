using CommunityToolkit.Maui.Core;
using CommunityToolkit.Maui.Views;
using OcwOffline.Models;
using OcwOffline.Services;
using OcwOffline.ViewModels;

namespace OcwOffline.Views;

/// <summary>
/// Code-behind only: points a MediaElement at one local file, persists
/// position on exit. Built per-lecture via Func&lt;VideoPlayerPage&gt;,
/// mirroring CatalogPage's Func&lt;CoursePage&gt;.
/// </summary>
public partial class VideoPlayerPage : ContentPage
{
    private readonly CourseDatabase _db;
    private Lecture? _lecture;

    public VideoPlayerPage(CourseDatabase db, AppStatusViewModel statusViewModel)
    {
        InitializeComponent();
        _db = db;
        StatusBanner.BindingContext = statusViewModel;
    }

    // Called by the caller right after resolving this page from the
    // factory, before Navigation.PushAsync. Mirrors how CatalogPage.xaml.cs
    // hands a freshly-resolved CoursePage its CourseSlug post-construction.
    public void LoadLecture(Lecture lecture)
    {
        _lecture = lecture;
        Title = lecture.Title;

        if (string.IsNullOrEmpty(lecture.LocalVideoPath))
        {
            LoadingIndicator.IsVisible = false;
            StatusLabel.Text = VideoPlaybackLogic.DetermineLoadStatus(lecture, fileExists: false);
            return;
        }

        var fullPath = Path.Combine(AppPaths.Root, lecture.LocalVideoPath);
        var fileExists = File.Exists(fullPath);
        // Same "flag it, don't fabricate a fix" posture as CourseViewModel
        // takes elsewhere in this codebase for a missing local file.
        StatusLabel.Text = VideoPlaybackLogic.DetermineLoadStatus(lecture, fileExists);
        if (!fileExists)
        {
            LoadingIndicator.IsVisible = false;
            return;
        }

        Player.Source = MediaSource.FromFile(fullPath);
    }

    // Seeking has to wait for MediaOpened: Duration/seek targets aren't
    // meaningful before the platform player has actually loaded the file.
    private async void OnMediaOpened(object sender, EventArgs e)
    {
        LoadingIndicator.IsVisible = false;
        if (_lecture is { LastWatchedPositionSeconds: > 0, IsCompleted: false })
        {
            await Player.SeekTo(TimeSpan.FromSeconds(_lecture.LastWatchedPositionSeconds), CancellationToken.None);
        }
    }

    private void OnMediaFailed(object sender, MediaFailedEventArgs e)
    {
        LoadingIndicator.IsVisible = false;
        Player.IsVisible = false;
        ErrorPanel.IsVisible = true;
    }

    private async void OnBackToLecturesClicked(object sender, EventArgs e) =>
        await Navigation.PopAsync();

    protected override async void OnDisappearing()
    {
        base.OnDisappearing();
        try
        {
            await SaveProgressAsync();
        }
        catch (Exception ex)
        {
            // Best-effort save must never crash navigation. SaveProgressAsync
            // already swallows its own DB faults; this guards the Player
            // property reads inside it (a torn-down player can throw on
            // Position/Duration) from escaping the async void override.
            System.Diagnostics.Debug.WriteLine($"SaveProgressAsync fault: {ex.GetType().Name}");
        }
    }

    // Best-effort watch-position save so reopening the same lecture can
    // resume. Skips lectures with no local file; any DB failure here
    // shouldn't block the person from navigating back.
    private async Task SaveProgressAsync()
    {
        if (_lecture is null || string.IsNullOrEmpty(_lecture.LocalVideoPath))
        {
            return;
        }

        var positionSeconds = (int)Player.Position.TotalSeconds;
        if (positionSeconds <= 0)
        {
            return;
        }

        _lecture.LastWatchedPositionSeconds = positionSeconds;
        var durationSeconds = (int)Player.Duration.TotalSeconds;
        var (newDuration, isCompleted) = VideoPlaybackLogic.DetermineCompletionState(positionSeconds, durationSeconds);
        if (newDuration is not null)
        {
            _lecture.DurationSeconds = newDuration.Value;
            _lecture.IsCompleted = isCompleted!.Value;
        }

        try
        {
            await _db.UpsertLectureAsync(_lecture);
        }
        catch
        {
            // Best-effort, per this method's summary above.
        }
    }
}
