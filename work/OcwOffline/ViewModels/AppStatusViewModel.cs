using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OcwOffline.Services;

namespace OcwOffline.ViewModels;

/// <summary>
/// App-wide status behind the persistent banner shown on every page:
/// the in-flight download roll-up and the offline indicator. Singleton;
/// every page binds its StatusBannerView to this instance. Property sets
/// go through the main-thread dispatcher because both events can arrive
/// off the UI thread.
/// </summary>
public partial class AppStatusViewModel : ObservableObject
{
    private readonly IMainThreadDispatcher _mainThread;

    public AppStatusViewModel(
        IDownloadManager downloads,
        IConnectivityService connectivity,
        IMainThreadDispatcher mainThread)
    {
        _mainThread = mainThread;

        IsOffline = !connectivity.IsConnected;
        UpdateFromAggregate(downloads.GetAggregate());

        downloads.AggregateChanged += aggregate =>
            _mainThread.BeginInvokeOnMainThread(() => UpdateFromAggregate(aggregate));
        connectivity.ConnectivityChanged += connected =>
            _mainThread.BeginInvokeOnMainThread(() => IsOffline = !connected);
    }

    [ObservableProperty]
    private bool hasActiveDownloads;

    [ObservableProperty]
    private string activeDownloadText = string.Empty;

    [ObservableProperty]
    private bool isOffline;

    // Pages that own a dashboard factory subscribe; pages without one
    // leave it unwired, so the tap simply does nothing there.
    public event Action? NavigateToDownloadsRequested;

    [RelayCommand]
    private void ShowActiveDownloads() => NavigateToDownloadsRequested?.Invoke();

    private void UpdateFromAggregate(DownloadAggregate aggregate)
    {
        HasActiveDownloads = aggregate.ActiveCount > 0;
        ActiveDownloadText = aggregate.ActiveCount > 0
            ? $"Downloading {aggregate.ActiveCount} {(aggregate.ActiveCount == 1 ? "item" : "items")}, {Math.Round(aggregate.OverallFraction * 100, MidpointRounding.AwayFromZero)}%"
            : string.Empty;
    }
}
