using OcwOffline.Services;

namespace OcwOffline.Tests.Fakes;

/// <summary>
/// Synchronous <see cref="IMainThreadDispatcher"/>. On a bare net10.0 host
/// MAUI's real MainThread throws NotImplementedInReferenceAssemblyException,
/// so progress callbacks run inline here instead.
/// </summary>
public sealed class FakeMainThreadDispatcher : IMainThreadDispatcher
{
    public void BeginInvokeOnMainThread(Action action) => action();
}
