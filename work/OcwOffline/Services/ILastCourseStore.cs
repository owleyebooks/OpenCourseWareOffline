namespace OcwOffline.Services;

/// <summary>
/// The last successfully loaded course id, so a returning user re-opens
/// where they left off. One persistence point for the whole app.
/// </summary>
public interface ILastCourseStore
{
    string? GetLastCourseId();
    void SetLastCourseId(string courseId);
    void Clear();
}
