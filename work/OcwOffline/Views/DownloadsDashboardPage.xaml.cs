using OcwOffline.Models;
using OcwOffline.ViewModels;

namespace OcwOffline.Views;

public partial class DownloadsDashboardPage : ContentPage
{
    private readonly DownloadsDashboardViewModel _vm;

    // Factory, not a shared instance: each lecture tapped needs its own
    // VideoPlayerPage pointed at its own file (same shape CoursePage uses).
    private readonly Func<VideoPlayerPage> _videoPlayerPageFactory;

    // Same factory shape again: each artifact tapped needs its own
    // ArtifactViewerPage pointed at its own file.
    private readonly Func<ArtifactViewerPage> _artifactViewerPageFactory;

    public DownloadsDashboardPage(
        DownloadsDashboardViewModel vm,
        AppStatusViewModel statusVm,
        Func<VideoPlayerPage> videoPlayerPageFactory,
        Func<ArtifactViewerPage> artifactViewerPageFactory)
    {
        InitializeComponent();
        BindingContext = vm;
        _vm = vm;
        StatusBanner.BindingContext = statusVm;
        _videoPlayerPageFactory = videoPlayerPageFactory;
        _artifactViewerPageFactory = artifactViewerPageFactory;
    }

    // The ViewModel is Singleton-registered (MauiProgram) while this Page
    // is Transient: the ViewModel's events would keep every visited page
    // alive, so unsubscribe in OnDisappearing and resubscribe in
    // OnAppearing. Construction-only subscription is not enough: pushing
    // a viewer fires OnDisappearing, and nothing would resubscribe after
    // returning. Unsubscribing first keeps this safe if Appearing ever
    // fires twice without a Disappearing between them.
    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _vm.OpenArtifactRequested -= OnOpenArtifactRequested;
        _vm.OpenLectureRequested -= OnOpenLectureRequested;
    }

    // Refresh every time the dashboard is navigated to, rather than only
    // once at construction: without this the numbers would go stale after
    // the first visit even though the Page itself is fresh each push.
    // In-flight progress between visits arrives live via AggregateChanged.
    protected override void OnAppearing()
    {
        base.OnAppearing();
        _ = ResubscribeAndReloadAsync();
    }

    private async Task ResubscribeAndReloadAsync()
    {
        _vm.OpenArtifactRequested -= OnOpenArtifactRequested;
        _vm.OpenLectureRequested -= OnOpenLectureRequested;
        _vm.OpenArtifactRequested += OnOpenArtifactRequested;
        _vm.OpenLectureRequested += OnOpenLectureRequested;
        await ReloadAsync();
    }

    // OnAppearing can't be async, and ICommand.Execute would swallow a
    // database fault raised inside LoadAsync (the ViewModel has no catch
    // of its own). Await ExecuteAsync instead so the failure reaches the
    // user rather than leaving a silently stale list.
    private async Task ReloadAsync()
    {
        try
        {
            await _vm.LoadCommand.ExecuteAsync(null);
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Couldn't refresh downloads", ex.Message, "OK");
        }
    }

    // PushAsync inside an async void event handler: an uncaught failure
    // (factory construction or the push itself) would crash the app.
    // Route it to an alert instead, mirroring CoursePage.
    private async void OnOpenArtifactRequested(Artifact artifact)
    {
        try
        {
            var viewer = _artifactViewerPageFactory();
            viewer.LoadArtifact(artifact);
            await Navigation.PushAsync(viewer);
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Couldn't open file", ex.Message, "OK");
        }
    }

    private async void OnOpenLectureRequested(Lecture lecture)
    {
        try
        {
            var player = _videoPlayerPageFactory();
            player.LoadLecture(lecture);
            await Navigation.PushAsync(player);
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Couldn't open video", ex.Message, "OK");
        }
    }

    private async void OnFindCourseClicked(object sender, EventArgs e)
    {
        try
        {
            await Navigation.PopToRootAsync();
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Couldn't go back", ex.Message, "OK");
        }
    }
}
