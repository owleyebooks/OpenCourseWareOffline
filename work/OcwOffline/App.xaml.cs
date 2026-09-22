using OcwOffline.Views;

namespace OcwOffline;

public partial class App : Application
{
    private readonly CoursePage _coursePage;

    public App(CoursePage coursePage)
    {
        // TEMPORARY diagnostic logging (screenshot-run branch only).
        System.Console.WriteLine("OCWSTARTUP: App ctor entry");
        InitializeComponent();
        System.Console.WriteLine("OCWSTARTUP: App InitializeComponent done");
        _coursePage = coursePage;
        System.Console.WriteLine("OCWSTARTUP: App ctor exit");
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        System.Console.WriteLine("OCWSTARTUP: CreateWindow entry");
        var window = new Window(new NavigationPage(_coursePage));
        System.Console.WriteLine("OCWSTARTUP: CreateWindow exit");
        return window;
    }
}
