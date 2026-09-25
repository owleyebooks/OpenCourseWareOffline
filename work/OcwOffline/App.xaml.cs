using OcwOffline.Services;
using OcwOffline.Views;

namespace OcwOffline;

public partial class App : Application
{
    private readonly CoursePage _coursePage;
    private readonly IConnectivityService _connectivityService;

    // The connectivity service is injected so its singleton is created
    // (and subscribed to the OS event) once at startup. The field keeps
    // the reference explicit; no page ever reads it directly.
    public App(CoursePage coursePage, IConnectivityService connectivityService)
    {
        InitializeComponent();
        _coursePage = coursePage;
        _connectivityService = connectivityService;
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        var window = new Window(new NavigationPage(_coursePage));
        // Explicit title: WinUI does not pick up ApplicationTitle on its
        // own for unpackaged apps, and tooling (including screenshot
        // capture) finds the window by this title.
        window.Title = "OCW Offline";
        return window;
    }
}
