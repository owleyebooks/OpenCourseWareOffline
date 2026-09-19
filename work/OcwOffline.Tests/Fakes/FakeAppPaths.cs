using OcwOffline.Services;

namespace OcwOffline.Tests.Fakes;

/// <summary>
/// <see cref="IAppPaths"/> double. FileSystem.AppDataDirectory throws
/// NotImplementedInReferenceAssemblyException on a bare net10.0 host, so
/// tests use an inert stand-in root. The zip-extraction test only asserts on
/// the destination subfolder, never on this root.
/// </summary>
public sealed class FakeAppPaths : IAppPaths
{
    public string Root => Path.Combine(Path.GetTempPath(), "ocw-offline-tests");
}
