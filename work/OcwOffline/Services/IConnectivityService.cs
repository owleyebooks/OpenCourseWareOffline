namespace OcwOffline.Services;

/// <summary>
/// App-level connectivity, wrapping MAUI's Connectivity so viewmodels stay
/// testable. The production implementation subscribes to
/// Connectivity.ConnectivityChanged once (singleton); tests use the fake.
/// No polling, no probing: only the OS event.
/// </summary>
public interface IConnectivityService
{
    bool IsConnected { get; }
    event Action<bool>? ConnectivityChanged;
}
