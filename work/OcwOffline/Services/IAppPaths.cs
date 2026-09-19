namespace OcwOffline.Services;

/// <summary>
/// Seam over the static <see cref="AppPaths"/> root. Same motivation as
/// <see cref="IMainThreadDispatcher"/>: <c>FileSystem.AppDataDirectory</c>
/// throws <c>NotImplementedInReferenceAssemblyException</c> on a bare net10.0
/// host, so production resolves <see cref="AppPathsProvider"/> and tests
/// inject a fake root.
/// </summary>
public interface IAppPaths
{
    string Root { get; }
}
