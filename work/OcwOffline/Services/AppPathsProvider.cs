namespace OcwOffline.Services;

/// <summary>
/// Production <see cref="IAppPaths"/>: delegates to the static
/// <see cref="AppPaths"/>, preserving on-device behavior exactly.
/// </summary>
public sealed class AppPathsProvider : IAppPaths
{
    public string Root => AppPaths.Root;
}
