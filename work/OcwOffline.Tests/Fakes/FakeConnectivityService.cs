using OcwOffline.Services;

namespace OcwOffline.Tests.Fakes;

/// <summary>
/// Scriptable IConnectivityService. Tests drive connectivity changes via
/// SetConnected instead of touching MAUI's Connectivity static.
/// </summary>
public class FakeConnectivityService : IConnectivityService
{
    private bool _isConnected = true;

    public bool IsConnected => _isConnected;

    public event Action<bool>? ConnectivityChanged;

    public void SetConnected(bool connected)
    {
        if (_isConnected == connected) return;
        _isConnected = connected;
        ConnectivityChanged?.Invoke(connected);
    }
}
