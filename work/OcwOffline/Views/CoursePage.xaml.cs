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

    public CoursePage(CourseViewModel vm, Func<DownloadsDashboardPage> dashboardPageFactory, Func<CatalogPage> catalogPageFactory,
        Func<VideoPlayerPage> videoPlayerPageFactory, Func<ArtifactViewerPage> artifactViewerPageFactory,
        Func<AboutPage> aboutPageFactory)
    {
        InitializeComponent();
        BindingContext = vm;
        _dashboardPageFactory = dashboardPageFactory;
        _catalogPageFactory = catalogPageFactory;
        _videoPlayerPageFactory = videoPlayerPageFactory;
        _artifactViewerPageFactory = artifactViewerPageFactory;
        _aboutPageFactory = aboutPageFactory;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        // TEMPORARY diagnostic (screenshot-run only): log MAUI-side layout
        // bounds to logcat so CI can verify the Grid fix even when the
        // emulator's System UI ANR makes screenshots and uiautomator
        // dumps unreliable. Remove before durable integration.
        Dispatcher.Dispatch(async () =>
        {
            try
            {
                await Task.Delay(3000);
                LogLayout("SearchEntry", SearchEntry);
                LogLayout("FetchButton", FetchButton);
                LogLayout("StatusLabel", StatusLabel);
                LogLayout("ResourcesTabButton", ResourcesTabButton);
                LogLayout("LecturesTabButton", LecturesTabButton);
                LogLayout("ArtifactsList", ArtifactsList);
                LogLayout("LecturesList", LecturesList);
                LogLayout("StorageLabel", StorageLabel);
                System.Console.WriteLine("OCWLAYOUT: done");
            }
            catch (System.Exception ex)
            {
                System.Console.WriteLine("OCWLAYOUT: EXCEPTION " + ex.GetType().Name + ": " + ex.Message);
            }
        });
    }

    private static void LogLayout(string name, VisualElement? view)
    {
        if (view is null)
        {
            System.Console.WriteLine($"OCWLAYOUT: {name} is null");
            return;
        }

        double x = 0;
        double y = 0;
        for (var e = view; e is not null; e = e.Parent as VisualElement)
        {
            x += e.X;
            y += e.Y;
        }

        System.Console.WriteLine($"OCWLAYOUT: {name} x={x:F0} y={y:F0} w={view.Width:F0} h={view.Height:F0} visible={view.IsVisible}");
    }

    private async void OnDownloadsClicked(object sender, EventArgs e)
    {
        await Navigation.PushAsync(_dashboardPageFactory());
    }

    private async void OnAboutClicked(object sender, EventArgs e)
    {
        await Navigation.PushAsync(_aboutPageFactory());
    }

    private async void OnBrowseCatalogClicked(object sender, EventArgs e)
    {
        await Navigation.PushAsync(_catalogPageFactory());
    }

    private async void OnWatchLectureClicked(object sender, EventArgs e)
    {
        if (sender is not Button { BindingContext: Lecture lecture })
        {
            return;
        }

        var player = _videoPlayerPageFactory();
        player.LoadLecture(lecture);
        await Navigation.PushAsync(player);
    }

    private async void OnViewArtifactClicked(object sender, EventArgs e)
    {
        if (sender is not Button { BindingContext: Artifact artifact })
        {
            return;
        }

        var viewer = _artifactViewerPageFactory();
        viewer.LoadArtifact(artifact);
        await Navigation.PushAsync(viewer);
    }
}
