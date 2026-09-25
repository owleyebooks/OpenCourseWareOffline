using Microsoft.Maui.Networking;

namespace OcwOffline.Services;

/// <summary>
/// Production IConnectivityService over MAUI's Connectivity. Subscribes to
/// ConnectivityChanged once (registered singleton); no polling, no
/// probing. Never construct this in tests: MAUI statics throw on a bare
/// net10.0 host. Tests use FakeConnectivityService instead.
/// </summary>
public sealed class MauiConnectivityService : IConnectivityService
{
    public MauiConnectivityService()
    {
        Connectivity.Current.ConnectivityChanged += OnConnectivityChanged;
    }

    public bool IsConnected => Connectivity.Current.NetworkAccess == NetworkAccess.Internet;

    public event Action<bool>? ConnectivityChanged;

    private void OnConnectivityChanged(object? sender, ConnectivityChangedEventArgs e) =>
        ConnectivityChanged?.Invoke(IsConnected);
}
