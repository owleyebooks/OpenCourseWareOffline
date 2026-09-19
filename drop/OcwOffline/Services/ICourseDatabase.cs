using OcwOffline.Models;

namespace OcwOffline.Services;

/// <summary>
/// CourseDatabase's public surface, extracted so consumers (ViewModels) can
/// depend on this instead of the concrete SQLite-backed class — lets tests
/// substitute an in-memory fake instead of real I/O. See AUDIT_TRAIL v33.
/// </summary>
public interface ICourseDatabase
{
    Task<List<Course>> GetAllCoursesAsync();
    Task<Course?> GetCourseAsync(string courseId);
    Task UpsertCourseAsync(Course course);

    Task<List<Artifact>> GetArtifactsForCourseAsync(string courseId);
    Task UpsertArtifactAsync(Artifact artifact);
    Task<Artifact?> FindArtifactBySourceUrlAsync(string courseId, string sourceUrl);

    Task<List<Lecture>> GetLecturesForCourseAsync(string courseId);
    Task UpsertLectureAsync(Lecture lecture);
    Task<Lecture?> FindLectureAsync(string courseId, string videoUrl, string title);
    Task UpdateWatchProgressAsync(int lectureId, int positionSeconds, bool isCompleted);

    Task<long> GetTotalBytesDownloadedAsync();
    Task DeleteCourseDataAsync(string courseId);
}
