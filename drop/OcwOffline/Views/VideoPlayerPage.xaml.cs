using CommunityToolkit.Maui.Views;
using OcwOffline.Models;
using OcwOffline.Services;

namespace OcwOffline.Views;

/// <summary>
/// Code-behind only — points a MediaElement at one local file, persists
/// position on exit. Built per-lecture via Func&lt;VideoPlayerPage&gt;,
/// mirroring CatalogPage's Func&lt;CoursePage&gt;. See AUDIT_TRAIL v17.
/// </summary>
public partial class VideoPlayerPage : ContentPage
{
    private readonly CourseDatabase _db;
    private Lecture? _lecture;

    public VideoPlayerPage(CourseDatabase db)
    {
        InitializeComponent();
        _db = db;
    }

    // Called by the caller right after resolving this page from the
    // factory, before Navigation.PushAsync — mirrors how CatalogPage.xaml.cs
    // hands a freshly-resolved CoursePage its CourseSlug post-construction.
    public void LoadLecture(Lecture lecture)
    {
        _lecture = lecture;
        Title = lecture.Title;

        if (string.IsNullOrEmpty(lecture.LocalVideoPath))
        {
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
            return;
        }

        Player.Source = MediaSource.FromFile(fullPath);
    }

    // Seeking has to wait for MediaOpened — Duration/seek targets aren't
    // meaningful before the platform player has actually loaded the file.
    private async void OnMediaOpened(object sender, EventArgs e)
    {
        if (_lecture is { LastWatchedPositionSeconds: > 0, IsCompleted: false })
        {
            await Player.SeekTo(TimeSpan.FromSeconds(_lecture.LastWatchedPositionSeconds), CancellationToken.None);
        }
    }

    private void OnMediaFailed(object sender, MediaFailedEventArgs e)
    {
        StatusLabel.Text = "Playback failed — the downloaded file may be corrupt or in an unsupported format.";
    }

    protected override async void OnDisappearing()
    {
        base.OnDisappearing();
        await SaveProgressAsync();
    }

    // Best-effort watch-position save so reopening the same lecture can
    // resume. Skips lectures with no local file; any DB failure here
    // shouldn't block the person from navigating back. See AUDIT_TRAIL v17
    // for why this isn't a bigger deal — no compiled build exists yet to
    // confirm SeekTo/Position/Duration actually behave as documented.
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
