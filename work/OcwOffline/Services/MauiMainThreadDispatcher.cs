using Microsoft.Maui.ApplicationModel;

namespace OcwOffline.Services;

/// <summary>
/// Production <see cref="IMainThreadDispatcher"/> backed by MAUI's real
/// <c>MainThread</c>. The explicit using (rather than relying on the app's
/// implicit MAUI usings) keeps this file compiling when file-linked into the
/// bare-net10.0 test project, where the call itself is never executed.
/// </summary>
public sealed class MauiMainThreadDispatcher : IMainThreadDispatcher
{
    public void BeginInvokeOnMainThread(Action action) =>
        MainThread.BeginInvokeOnMainThread(action);
}
