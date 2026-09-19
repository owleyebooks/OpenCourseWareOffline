namespace OcwOffline.Services;

/// <summary>
/// Documents/ root, not Library/. FileSystem.AppDataDirectory resolves to
/// Library on iOS, which is invisible to the Files app. Direct NSFileManager,
/// not Environment.SpecialFolder.MyDocuments (unstable across iOS eras).
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
