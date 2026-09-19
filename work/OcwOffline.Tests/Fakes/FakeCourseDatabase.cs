using OcwOffline.Models;
using OcwOffline.Services;

namespace OcwOffline.Tests.Fakes;

/// <summary>
/// In-memory stand-in for CourseDatabase: same public contract, no SQLite,
/// no filesystem. Public collections are for test setup/assertions, not
/// part of ICourseDatabase.
/// </summary>
public class FakeCourseDatabase : ICourseDatabase
{
    public readonly Dictionary<string, Course> Courses = new();
    public readonly List<Artifact> Artifacts = new();
    public readonly List<Lecture> Lectures = new();

    private int _nextArtifactId = 1;
    private int _nextLectureId = 1;

    public Task<List<Course>> GetAllCoursesAsync() =>
        Task.FromResult(Courses.Values.ToList());

    public Task<Course?> GetCourseAsync(string courseId) =>
        Task.FromResult(Courses.TryGetValue(courseId, out var c) ? c : null);

    public Task UpsertCourseAsync(Course course)
    {
        Courses[course.Id] = course;
        return Task.CompletedTask;
    }

    public Task<List<Artifact>> GetArtifactsForCourseAsync(string courseId) =>
        Task.FromResult(Artifacts.Where(a => a.CourseId == courseId).ToList());

    public Task UpsertArtifactAsync(Artifact artifact)
    {
        if (artifact.Id == 0)
        {
            artifact.Id = _nextArtifactId++;
            Artifacts.Add(artifact);
        }
        else
        {
            var index = Artifacts.FindIndex(a => a.Id == artifact.Id);
            if (index >= 0) Artifacts[index] = artifact;
            else Artifacts.Add(artifact);
        }
        return Task.CompletedTask;
    }

    public Task<Artifact?> FindArtifactBySourceUrlAsync(string courseId, string sourceUrl) =>
        Task.FromResult(Artifacts.FirstOrDefault(a => a.CourseId == courseId && a.SourceUrl == sourceUrl));

    public Task<List<Lecture>> GetLecturesForCourseAsync(string courseId) =>
        Task.FromResult(Lectures.Where(l => l.CourseId == courseId).ToList());

    public Task UpsertLectureAsync(Lecture lecture)
    {
        if (lecture.Id == 0)
        {
            lecture.Id = _nextLectureId++;
            Lectures.Add(lecture);
        }
        else
        {
            var index = Lectures.FindIndex(l => l.Id == lecture.Id);
            if (index >= 0) Lectures[index] = lecture;
            else Lectures.Add(lecture);
        }
        return Task.CompletedTask;
    }

    // Mirrors CourseDatabase.FindLectureAsync exactly: VideoUrl match when
    // present, else Title + empty-VideoUrl fallback (YouTube-only lectures).
    public Task<Lecture?> FindLectureAsync(string courseId, string videoUrl, string title)
    {
        if (!string.IsNullOrWhiteSpace(videoUrl))
            return Task.FromResult(Lectures.FirstOrDefault(l => l.CourseId == courseId && l.VideoUrl == videoUrl));

        return Task.FromResult(Lectures.FirstOrDefault(
            l => l.CourseId == courseId && l.Title == title && l.VideoUrl == ""));
    }

    public Task UpdateWatchProgressAsync(int lectureId, int positionSeconds, bool isCompleted)
    {
        var lecture = Lectures.FirstOrDefault(l => l.Id == lectureId);
        if (lecture is null) return Task.CompletedTask;

        lecture.LastWatchedPositionSeconds = positionSeconds;
        lecture.IsCompleted = isCompleted;
        return Task.CompletedTask;
    }

    public Task<long> GetTotalBytesDownloadedAsync() =>
        Task.FromResult(
            Artifacts.Where(a => a.DownloadStatus == DownloadStatus.Completed).Sum(a => a.FileSizeBytes) +
            Lectures.Where(l => l.DownloadStatus == DownloadStatus.Completed).Sum(l => l.FileSizeBytes));

    public Task DeleteCourseDataAsync(string courseId)
    {
        Artifacts.RemoveAll(a => a.CourseId == courseId);
        Lectures.RemoveAll(l => l.CourseId == courseId);
        Courses.Remove(courseId);
        return Task.CompletedTask;
    }
}
