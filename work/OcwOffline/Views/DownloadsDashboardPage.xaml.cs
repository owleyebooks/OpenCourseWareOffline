using OcwOffline.ViewModels;

namespace OcwOffline.Views;

public partial class DownloadsDashboardPage : ContentPage
{
    private readonly DownloadsDashboardViewModel _vm;

    public DownloadsDashboardPage(DownloadsDashboardViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
        _vm = vm;
    }

    // Refresh every time the dashboard is navigated to, rather than only
    // once at construction. The ViewModel is Singleton-registered
    // (MauiProgram), so without this the numbers would go stale after
    // the first visit even though the Page itself is fresh each push.
    protected override void OnAppearing()
    {
        base.OnAppearing();
        _vm.LoadCommand.Execute(null);
    }
}
