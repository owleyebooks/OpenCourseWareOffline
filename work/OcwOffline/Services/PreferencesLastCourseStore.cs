namespace OcwOffline.Services;

/// <summary>
/// ILastCourseStore backed by MAUI Preferences. Never constructed in
/// tests: Preferences.Default throws on a bare net10.0 host, same as the
/// other MAUI statics (see IMainThreadDispatcher). Tests use
/// InMemoryLastCourseStore.
/// </summary>
public class PreferencesLastCourseStore : ILastCourseStore
{
    private const string Key = "ocw.lastCourseId";

    public string? GetLastCourseId() => Preferences.Default.Get<string?>(Key, null);
    public void SetLastCourseId(string courseId) => Preferences.Default.Set(Key, courseId);
    public void Clear() => Preferences.Default.Remove(Key);
}
