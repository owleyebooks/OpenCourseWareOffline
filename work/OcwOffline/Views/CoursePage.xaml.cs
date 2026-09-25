using OcwOffline.Models;
using OcwOffline.ViewModels;

namespace OcwOffline.Views;

public partial class CoursePage : ContentPage
{
    // Factory, not a shared instance. A Page can't be pushed onto Navigation
    // twice while still parented elsewhere in the stack. ViewModel underneath
    // is still the shared Singleton, so browse/download state survives across
    // pushes; only the Page shell is fresh each time.
    private readonly Func<DownloadsDashboardPage> _dashboardPageFactory;
    private readonly Func<CatalogPage> _catalogPageFactory;

    // Factory, not a shared instance: each lecture tapped needs its own
    // VideoPlayerPage pointed at its own file, same reasoning CatalogPage
    // already applies to its own CoursePage factory.
    private readonly Func<VideoPlayerPage> _videoPlayerPageFactory;

    // Same factory shape again for the same reason: each artifact tapped
    // needs its own ArtifactViewerPage pointed at its own file. Added v18.
    private readonly Func<ArtifactViewerPage> _artifactViewerPageFactory;

    // Same again: the About page is pushed from the toolbar, never pushed
    // twice at once, so it takes the factory rather than a shared instance.
    private readonly Func<AboutPage> _aboutPageFactory;

    public CoursePage(CourseViewModel vm, AppStatusViewModel appStatus, Func<DownloadsDashboardPage> dashboardPageFactory, Func<CatalogPage> catalogPageFactory,
        Func<VideoPlayerPage> videoPlayerPageFactory, Func<ArtifactViewerPage> artifactViewerPageFactory,
        Func<AboutPage> aboutPageFactory)
    {
        InitializeComponent();
        BindingContext = vm;
        StatusBanner.BindingContext = appStatus;
        _dashboardPageFactory = dashboardPageFactory;
        _catalogPageFactory = catalogPageFactory;
        _videoPlayerPageFactory = videoPlayerPageFactory;
        _artifactViewerPageFactory = artifactViewerPageFactory;
        _aboutPageFactory = aboutPageFactory;

        vm.NavigateToDownloadsRequested += OnNavigateToDownloadsRequested;
        vm.RefocusCourseEntryRequested += OnRefocusCourseEntryRequested;
        vm.OpenArtifactRequested += OnOpenArtifactRequested;
        vm.OpenLectureRequested += OnOpenLectureRequested;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        // Restores the last-viewed course when there is connectivity; the
        // ViewModel guards this to run once. Offline, the first-run intro
        // stays put instead of an error screen.
        if (BindingContext is CourseViewModel vm)
            _ = vm.RestoreLastCourseAsync();
    }

    // PushAsync inside an async void event handler: an uncaught failure
    // (factory construction or the push itself) would crash the app.
    // Route it to an alert instead.
    private async Task PushSafelyAsync(Func<Page> pageFactory, string pageName)
    {
        try
        {
            await Navigation.PushAsync(pageFactory());
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync($"Couldn't open {pageName}", ex.Message, "OK");
        }
    }

    private async void OnDownloadsClicked(object sender, EventArgs e)
    {
        await PushSafelyAsync(_dashboardPageFactory, "downloads");
    }

    private async void OnAboutClicked(object sender, EventArgs e)
    {
        await PushSafelyAsync(_aboutPageFactory, "about");
    }

    private async void OnBrowseCatalogClicked(object sender, EventArgs e)
    {
        await PushSafelyAsync(_catalogPageFactory, "catalog");
    }

    private async void OnNavigateToDownloadsRequested() =>
        await PushSafelyAsync(_dashboardPageFactory, "downloads");

    private void OnRefocusCourseEntryRequested() => SearchEntry.Focus();

    private async void OnOpenArtifactRequested(Artifact artifact) =>
        await OpenArtifactAsync(artifact);

    private async void OnOpenLectureRequested(Lecture lecture) =>
        await OpenLectureAsync(lecture);

    private async Task OpenLectureAsync(Lecture lecture)
    {
        try
        {
            var player = _videoPlayerPageFactory();
            player.LoadLecture(lecture);
            await Navigation.PushAsync(player);
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Couldn't open lecture", ex.Message, "OK");
        }
    }

    private async Task OpenArtifactAsync(Artifact artifact)
    {
        try
        {
            var viewer = _artifactViewerPageFactory();
            viewer.LoadArtifact(artifact);
            await Navigation.PushAsync(viewer);
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Couldn't open artifact", ex.Message, "OK");
        }
    }
}
