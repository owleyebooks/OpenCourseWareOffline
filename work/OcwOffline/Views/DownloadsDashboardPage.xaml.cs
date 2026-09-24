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
        _ = ReloadAsync();
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
}
