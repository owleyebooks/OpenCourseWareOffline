using OcwOffline.Models;
using OcwOffline.Tests.Fakes;

namespace OcwOffline.Tests.Services;

public class FakeCourseDatabaseTests
{
    [Fact]
    public async Task UpsertCourseAsync_NewCourse_IsRetrievableByGetCourseAsync()
    {
        var db = new FakeCourseDatabase();
        var course = new Course { Id = "hst-508", Title = "Genomics" };

        await db.UpsertCourseAsync(course);
        var result = await db.GetCourseAsync("hst-508");

        Assert.NotNull(result);
        Assert.Equal("Genomics", result!.Title);
    }

    [Fact]
    public async Task UpsertCourseAsync_SameId_OverwritesExisting()
    {
        var db = new FakeCourseDatabase();
        await db.UpsertCourseAsync(new Course { Id = "hst-508", Title = "Old Title" });

        await db.UpsertCourseAsync(new Course { Id = "hst-508", Title = "New Title" });
        var all = await db.GetAllCoursesAsync();

        Assert.Single(all);
        Assert.Equal("New Title", all[0].Title);
    }

    [Fact]
    public async Task GetCourseAsync_UnknownId_ReturnsNull()
    {
        var db = new FakeCourseDatabase();

        var result = await db.GetCourseAsync("nonexistent");

        Assert.Null(result);
    }

    [Fact]
    public async Task UpsertArtifactAsync_NewArtifact_AssignsIncrementingIds()
    {
        var db = new FakeCourseDatabase();
        var first = new Artifact { CourseId = "c1", SourceUrl = "https://a" };
        var second = new Artifact { CourseId = "c1", SourceUrl = "https://b" };

        await db.UpsertArtifactAsync(first);
        await db.UpsertArtifactAsync(second);

        Assert.Equal(1, first.Id);
        Assert.Equal(2, second.Id);
    }

    [Fact]
    public async Task UpsertArtifactAsync_ExistingId_UpdatesInPlace()
    {
        var db = new FakeCourseDatabase();
        var artifact = new Artifact { CourseId = "c1", SourceUrl = "https://a", Title = "Old" };
        await db.UpsertArtifactAsync(artifact);

        artifact.Title = "New";
        await db.UpsertArtifactAsync(artifact);
        var results = await db.GetArtifactsForCourseAsync("c1");

        Assert.Single(results);
        Assert.Equal("New", results[0].Title);
    }

    [Fact]
    public async Task FindArtifactBySourceUrlAsync_MatchesCourseIdAndSourceUrl()
    {
        var db = new FakeCourseDatabase();
        await db.UpsertArtifactAsync(new Artifact { CourseId = "c1", SourceUrl = "https://a" });

        var match = await db.FindArtifactBySourceUrlAsync("c1", "https://a");
        var noMatch = await db.FindArtifactBySourceUrlAsync("c2", "https://a");

        Assert.NotNull(match);
        Assert.Null(noMatch);
    }

    [Fact]
    public async Task FindLectureAsync_VideoUrlPresent_MatchesOnVideoUrl()
    {
        var db = new FakeCourseDatabase();
        await db.UpsertLectureAsync(new Lecture { CourseId = "c1", VideoUrl = "https://v", Title = "Lec 1" });

        var result = await db.FindLectureAsync("c1", "https://v", "Lec 1");

        Assert.NotNull(result);
    }

    [Fact]
    public async Task FindLectureAsync_BlankVideoUrl_FallsBackToTitleMatch()
    {
        var db = new FakeCourseDatabase();
        await db.UpsertLectureAsync(new Lecture { CourseId = "c1", VideoUrl = "", Title = "YouTube Only" });

        var result = await db.FindLectureAsync("c1", videoUrl: "", title: "YouTube Only");

        Assert.NotNull(result);
    }

    [Fact]
    public async Task UpdateWatchProgressAsync_ExistingLecture_UpdatesFields()
    {
        var db = new FakeCourseDatabase();
        var lecture = new Lecture { CourseId = "c1", Title = "Lec 1" };
        await db.UpsertLectureAsync(lecture);

        await db.UpdateWatchProgressAsync(lecture.Id, 120, isCompleted: true);

        Assert.Equal(120, lecture.LastWatchedPositionSeconds);
        Assert.True(lecture.IsCompleted);
    }

    [Fact]
    public async Task UpdateWatchProgressAsync_UnknownLectureId_NoOp()
    {
        var db = new FakeCourseDatabase();

        await db.UpdateWatchProgressAsync(999, 60, true); // should not throw
    }

    [Fact]
    public async Task GetTotalBytesDownloadedAsync_SumsOnlyCompletedItems()
    {
        var db = new FakeCourseDatabase();
        await db.UpsertArtifactAsync(new Artifact { CourseId = "c1", DownloadStatus = DownloadStatus.Completed, FileSizeBytes = 100 });
        await db.UpsertArtifactAsync(new Artifact { CourseId = "c1", DownloadStatus = DownloadStatus.InProgress, FileSizeBytes = 500 });
        await db.UpsertLectureAsync(new Lecture { CourseId = "c1", DownloadStatus = DownloadStatus.Completed, FileSizeBytes = 50 });

        var total = await db.GetTotalBytesDownloadedAsync();

        Assert.Equal(150, total);
    }

    [Fact]
    public async Task DeleteCourseDataAsync_RemovesCourseArtifactsAndLectures_LeavesOtherCoursesIntact()
    {
        var db = new FakeCourseDatabase();
        await db.UpsertCourseAsync(new Course { Id = "c1" });
        await db.UpsertCourseAsync(new Course { Id = "c2" });
        await db.UpsertArtifactAsync(new Artifact { CourseId = "c1", SourceUrl = "https://a" });
        await db.UpsertArtifactAsync(new Artifact { CourseId = "c2", SourceUrl = "https://b" });
        await db.UpsertLectureAsync(new Lecture { CourseId = "c1" });

        await db.DeleteCourseDataAsync("c1");

        Assert.Null(await db.GetCourseAsync("c1"));
        Assert.NotNull(await db.GetCourseAsync("c2"));
        Assert.Empty(await db.GetArtifactsForCourseAsync("c1"));
        Assert.Empty(await db.GetLecturesForCourseAsync("c1"));
        Assert.Single(await db.GetArtifactsForCourseAsync("c2"));
    }
}
