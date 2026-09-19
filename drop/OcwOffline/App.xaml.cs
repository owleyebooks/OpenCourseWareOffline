using OcwOffline.Views;

namespace OcwOffline;

public partial class App : Application
{
    public App(CoursePage coursePage)
    {
        InitializeComponent();
        MainPage = new NavigationPage(coursePage);
    }
}
