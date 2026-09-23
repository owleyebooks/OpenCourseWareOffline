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
        // TEMPORARY diagnostic (screenshot-run only): is the logical tree complete?
        try
        {
            if (Content is ScrollView sv && sv.Content is VerticalStackLayout vsl)
            {
                System.Console.WriteLine($"OCWDIAG: vsl children={vsl.Children.Count}");
                foreach (var c in vsl.Children)
                    System.Console.WriteLine($"OCWDIAG: child={c.GetType().Name} handler={c.Handler is not null} visible={c.IsVisible}");
            }
            else
            {
                System.Console.WriteLine($"OCWDIAG: unexpected content shape: {Content?.GetType().Name}");
            }
        }
        catch (System.Exception ex)
        {
            System.Console.WriteLine("OCWDIAG: EXCEPTION " + ex.GetType().Name + ": " + ex.Message);
        }
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
