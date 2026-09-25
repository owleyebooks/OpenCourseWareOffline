namespace OcwOffline.Services;

/// <summary>
/// In-memory ILastCourseStore. Used by tests; production uses
/// PreferencesLastCourseStore.
/// </summary>
public class InMemoryLastCourseStore : ILastCourseStore
{
    private string? _courseId;

    public string? GetLastCourseId() => _courseId;
    public void SetLastCourseId(string courseId) => _courseId = courseId;
    public void Clear() => _courseId = null;
}
