namespace OcwOffline.Services;

/// <summary>
/// Documents/ root, not Library/ — FileSystem.AppDataDirectory resolves to
/// Library on iOS, invisible to Files app. Direct NSFileManager, not
/// Environment.SpecialFolder.MyDocuments (unstable across iOS eras). See AUDIT_TRAIL v16.
/// </summary>
public static class AppPaths
{
    public static string Root
    {
        get
        {
#if IOS
            return Foundation.NSFileManager.DefaultManager.GetUrls(
                Foundation.NSSearchPathDirectory.DocumentDirectory,
                Foundation.NSSearchPathDomain.User)[0].Path!;
#else
            return FileSystem.AppDataDirectory;
#endif
        }
    }
}
