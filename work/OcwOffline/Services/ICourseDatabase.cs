using OcwOffline.Models;

namespace OcwOffline.Services;

/// <summary>
/// CourseDatabase's public surface, extracted so consumers (ViewModels) can
/// depend on this instead of the concrete SQLite-backed class. Lets tests
/// substitute an in-memory fake instead of real I/O.
/// </summary>
public interface ICourseDatabase
{
    Task<List<Course>> GetAllCoursesAsync();
    Task<Course?> GetCourseAsync(string courseId);
    Task UpsertCourseAsync(Course course);

    Task<List<Artifact>> GetArtifactsForCourseAsync(string courseId);
    Task<Artifact?> GetArtifactAsync(int artifactId);
    Task UpsertArtifactAsync(Artifact artifact);
    Task DeleteArtifactAsync(int artifactId);
    Task<Artifact?> FindArtifactBySourceUrlAsync(string courseId, string sourceUrl);

    Task<List<Lecture>> GetLecturesForCourseAsync(string courseId);
    Task<Lecture?> GetLectureAsync(int lectureId);
    Task UpsertLectureAsync(Lecture lecture);
    Task DeleteLectureAsync(int lectureId);
    Task<Lecture?> FindLectureAsync(string courseId, string videoUrl, string title);
    Task UpdateWatchProgressAsync(int lectureId, int positionSeconds, bool isCompleted);

    Task<long> GetTotalBytesDownloadedAsync();
    Task DeleteCourseDataAsync(string courseId);
}
