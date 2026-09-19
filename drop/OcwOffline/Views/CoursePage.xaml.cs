using OcwOffline.Models;
using OcwOffline.ViewModels;

namespace OcwOffline.Views;

public partial class CoursePage : ContentPage
{
    // Factory, not a shared instance — a Page can't be pushed onto Navigation
    // twice while still parented elsewhere in the stack. ViewModel underneath
    // is still the shared Singleton, so browse/download state survives across
    // pushes; only the Page shell is fresh each time. See AUDIT_TRAIL v27.
    private readonly Func<DownloadsDashboardPage> _dashboardPageFactory;
    private readonly Func<CatalogPage> _catalogPageFactory;

    // Factory, not a shared instance — each lecture tapped needs its own
    // VideoPlayerPage pointed at its own file, same reasoning CatalogPage
    // already applies to its own CoursePage factory. See AUDIT_TRAIL v17.
    private readonly Func<VideoPlayerPage> _videoPlayerPageFactory;

    // Same factory shape again for the same reason: each artifact tapped
    // needs its own ArtifactViewerPage pointed at its own file. Added v18.
    private readonly Func<ArtifactViewerPage> _artifactViewerPageFactory;

    public CoursePage(CourseViewModel vm, Func<DownloadsDashboardPage> dashboardPageFactory, Func<CatalogPage> catalogPageFactory,
        Func<VideoPlayerPage> videoPlayerPageFactory, Func<ArtifactViewerPage> artifactViewerPageFactory)
    {
        InitializeComponent();
        BindingContext = vm;
        _dashboardPageFactory = dashboardPageFactory;
        _catalogPageFactory = catalogPageFactory;
        _videoPlayerPageFactory = videoPlayerPageFactory;
        _artifactViewerPageFactory = artifactViewerPageFactory;
    }

    private async void OnDownloadsClicked(object sender, EventArgs e)
    {
        await Navigation.PushAsync(_dashboardPageFactory());
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
