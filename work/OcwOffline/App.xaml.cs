using OcwOffline.Views;

namespace OcwOffline;

public partial class App : Application
{
    private readonly CoursePage _coursePage;

    public App(CoursePage coursePage)
    {
        InitializeComponent();
        _coursePage = coursePage;
    }

    protected override Window CreateWindow(IActivationState? activationState) =>
        new(new NavigationPage(_coursePage));
}
