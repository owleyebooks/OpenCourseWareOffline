namespace OcwOffline.Services;

/// <summary>
/// Seam over MAUI's static <c>MainThread</c> dispatch. ViewModels must not call
/// the static directly: on a bare net10.0 host (unit tests) every
/// <c>MainThread</c> member throws
/// <c>NotImplementedInReferenceAssemblyException</c>. Production resolves
/// <see cref="MauiMainThreadDispatcher"/>; tests inject a synchronous fake.
/// </summary>
public interface IMainThreadDispatcher
{
    void BeginInvokeOnMainThread(Action action);
}
