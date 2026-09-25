using System.Linq;
using OcwOffline.Models;
using OcwOffline.Tests.Fakes;
using OcwOffline.ViewModels;

namespace OcwOffline.Tests.ViewModels;

public class DownloadsDashboardViewModelCommandTests
{
    private static DownloadsDashboardViewModel CreateViewModel(out FakeCourseDatabase db, out FakeDownloadManager downloads)
    {
        db = new FakeCourseDatabase();
        downloads = new FakeDownloadManager();
        return new DownloadsDashboardViewModel(db, downloads, new FakeMainThreadDispatcher(), new FakeAppPaths());
    }

    [Fact]
    public async Task LoadAsync_SkipsCoursesWithNothingOnDisk()
    {
        var vm = CreateViewModel(out var db, out var downloads);
        await db.UpsertCourseAsync(new Course { Id = "c1", Title = "Has Files" });
        await db.UpsertCourseAsync(new Course { Id = "c2", Title = "Nothing Downloaded" });
        downloads.QueueSuccess("seed", 300, "c1/f");
        await downloads.DownloadAsync("https://a", "c1", "f", "seed");

        await vm.LoadCommand.ExecuteAsync(null);

        vm.Courses.Should().ContainSingle("only the course with files on disk is listed");
        Assert.Equal("c1", vm.Courses[0].CourseId);
        vm.StorageSummary.Should().Be(
            "Using 300 bytes across 1 course",
            "the skipped course contributes nothing to the summary, and one course reads singular");
    }

    [Fact]
    public async Task LoadAsync_BlankTitle_FallsBackToCourseId()
    {
        var vm = CreateViewModel(out var db, out var downloads);
        await db.UpsertCourseAsync(new Course { Id = "c1", Title = "" });
        downloads.QueueSuccess("seed", 50, "c1/f");
        await downloads.DownloadAsync("https://a", "c1", "f", "seed");

        await vm.LoadCommand.ExecuteAsync(null);

        Assert.Equal("c1", vm.Courses[0].Title);
    }

    [Fact]
    public async Task LoadAsync_TwoCourses_StorageSummaryUsesPlural()
    {
        var vm = CreateViewModel(out var db, out var downloads);
        await db.UpsertCourseAsync(new Course { Id = "c1", Title = "One" });
        await db.UpsertCourseAsync(new Course { Id = "c2", Title = "Two" });
        downloads.QueueSuccess("s1", 300, "c1/f");
        await downloads.DownloadAsync("https://a", "c1", "f", "s1");
        downloads.QueueSuccess("s2", 150, "c2/f");
        await downloads.DownloadAsync("https://b", "c2", "f", "s2");

        await vm.LoadCommand.ExecuteAsync(null);

        vm.StorageSummary.Should().Be("Using 450 bytes across 2 courses");
    }

    [Fact]
    public async Task DeleteCourseAsync_RemovesRowAndUpdatesSummary_LeavesOtherCourseIntact()
    {
        var vm = CreateViewModel(out var db, out var downloads);
        await db.UpsertCourseAsync(new Course { Id = "c1" });
        await db.UpsertCourseAsync(new Course { Id = "c2" });
        downloads.QueueSuccess("s1", 300, "c1/f");
        await downloads.DownloadAsync("https://a", "c1", "f", "s1");
        downloads.QueueSuccess("s2", 150, "c2/f");
        await downloads.DownloadAsync("https://b", "c2", "f", "s2");
        await vm.LoadCommand.ExecuteAsync(null);
        var courseRow = vm.Courses.Single(c => c.CourseId == "c1");

        await vm.DeleteCourseCommand.ExecuteAsync(courseRow);

        Assert.DoesNotContain(vm.Courses, c => c.CourseId == "c1");
        vm.StorageSummary.Should().Be(
            "Using 150 bytes across 1 course",
            "deleting c1 must leave only c2's 150 bytes counted");
        Assert.Null(await db.GetCourseAsync("c1"));
        (await downloads.GetStorageUsedByCourseAsync("c1")).Should().Be(0, "deleting the course must release its downloaded bytes");
    }

    [Fact]
    public async Task CourseRow_ToggleExpandedCommand_FlipsIsExpanded()
    {
        var vm = CreateViewModel(out var db, out var downloads);
        await db.UpsertCourseAsync(new Course { Id = "c1", Title = "T" });
        downloads.QueueSuccess("seed", 300, "c1/f");
        await downloads.DownloadAsync("https://a", "c1", "f", "seed");
        await vm.LoadCommand.ExecuteAsync(null);
        var course = vm.Courses.Should().ContainSingle().Which;

        course.IsExpanded.Should().BeFalse("rows start collapsed");
        course.ToggleExpandedCommand.Execute(null);
        course.IsExpanded.Should().BeTrue("tapping the row expands it");
        course.ToggleExpandedCommand.Execute(null);
        course.IsExpanded.Should().BeFalse("tapping again collapses it");
    }

    [Fact]
    public async Task LoadAsync_PopulatesDownloadedItemsWithKindSizeAndOpenLabel()
    {
        var vm = CreateViewModel(out var db, out var downloads);
        await db.UpsertCourseAsync(new Course { Id = "c1", Title = "T" });
        await db.UpsertArtifactAsync(new Artifact
        {
            CourseId = "c1", Title = "Syllabus", SourceUrl = "https://a/s.pdf",
            FileType = ArtifactFileType.Pdf, FileSizeBytes = 2048,
            LocalFilePath = "c1/s.pdf", DownloadStatus = DownloadStatus.Completed
        });
        await db.UpsertLectureAsync(new Lecture
        {
            CourseId = "c1", Title = "Lec 1", VideoUrl = "https://a/l.mp4",
            FileSizeBytes = 5 * 1024 * 1024,
            LocalVideoPath = "c1/l.mp4", DownloadStatus = DownloadStatus.Completed
        });
        downloads.QueueSuccess("s1", 2048, "c1/s.pdf");
        await downloads.DownloadAsync("https://a", "c1", "s.pdf", "s1");
        downloads.QueueSuccess("s2", 5 * 1024 * 1024, "c1/l.mp4");
        await downloads.DownloadAsync("https://a", "c1", "l.mp4", "s2");

        await vm.LoadCommand.ExecuteAsync(null);

        var course = vm.Courses.Should().ContainSingle().Which;
        course.Items.Should().HaveCount(2, "both downloaded items are listed under the course");
        var document = course.Items.Should().ContainSingle(i => i.Kind == "Document").Which;
        document.Title.Should().Be("Syllabus");
        document.SizeText.Should().Be("2 KB", "sizes render human-readable");
        document.OpenLabel.Should().Be("Open", "documents open");
        var video = course.Items.Should().ContainSingle(i => i.Kind == "Video").Which;
        video.SizeText.Should().Be("5 MB");
        video.OpenLabel.Should().Be("Watch", "videos are watched, not opened");
    }

    [Fact]
    public async Task LoadAsync_SkipsItemsThatWereNeverDownloaded()
    {
        var vm = CreateViewModel(out var db, out var downloads);
        await db.UpsertCourseAsync(new Course { Id = "c1", Title = "T" });
        await db.UpsertArtifactAsync(new Artifact
        {
            CourseId = "c1", Title = "Not downloaded", SourceUrl = "https://a/x.pdf",
            FileType = ArtifactFileType.Pdf, DownloadStatus = DownloadStatus.NotStarted
        });
        downloads.QueueSuccess("seed", 100, "c1/f");
        await downloads.DownloadAsync("https://a", "c1", "f", "seed");

        await vm.LoadCommand.ExecuteAsync(null);

        vm.Courses.Should().ContainSingle().Which.Items.Should().BeEmpty(
            "an item with no local file is not a downloaded item");
    }

    [Fact]
    public async Task DeleteItem_RemovesFileAndDbRowAndRefreshesSummary()
    {
        var vm = CreateViewModel(out var db, out var downloads);
        await db.UpsertCourseAsync(new Course { Id = "c1", Title = "T" });
        await db.UpsertArtifactAsync(new Artifact
        {
            CourseId = "c1", Title = "Doc", SourceUrl = "https://a/d.pdf",
            FileType = ArtifactFileType.Pdf, FileSizeBytes = 300,
            LocalFilePath = "c1/d.pdf", DownloadStatus = DownloadStatus.Completed
        });
        var lecture = new Lecture
        {
            CourseId = "c1", Title = "Lec", VideoUrl = "https://a/l.mp4",
            FileSizeBytes = 150, LocalVideoPath = "c1/l.mp4", DownloadStatus = DownloadStatus.Completed
        };
        await db.UpsertLectureAsync(lecture);
        downloads.QueueSuccess("s1", 300, "c1/d.pdf");
        await downloads.DownloadAsync("https://a", "c1", "d.pdf", "s1");
        downloads.QueueSuccess("s2", 150, "c1/l.mp4");
        await downloads.DownloadAsync("https://a", "c1", "l.mp4", "s2");
        await vm.LoadCommand.ExecuteAsync(null);
        var course = vm.Courses.Should().ContainSingle().Which;
        var item = course.Items.Should().ContainSingle(i => i.Kind == "Video").Which;

        await item.DeleteCommand.ExecuteAsync(null);

        (await db.GetLectureAsync(lecture.Id)).Should().BeNull("delete removes the DB row");
        (await downloads.GetStorageUsedByCourseAsync("c1")).Should().Be(300, "delete removes only that file's bytes");
        course.Items.Should().ContainSingle("the deleted item leaves the row");
        course.BytesUsed.Should().Be(300, "the course row reflects the remaining bytes");
        vm.StorageSummary.Should().Be("Using 300 bytes across 1 course");
    }

    [Fact]
    public async Task DeleteItem_LastItemOfCourse_RemovesTheCourseRow()
    {
        var vm = CreateViewModel(out var db, out var downloads);
        await db.UpsertCourseAsync(new Course { Id = "c1", Title = "T" });
        var lecture = new Lecture
        {
            CourseId = "c1", Title = "Lec", VideoUrl = "https://a/l.mp4",
            FileSizeBytes = 150, LocalVideoPath = "c1/l.mp4", DownloadStatus = DownloadStatus.Completed
        };
        await db.UpsertLectureAsync(lecture);
        downloads.QueueSuccess("s1", 150, "c1/l.mp4");
        await downloads.DownloadAsync("https://a", "c1", "l.mp4", "s1");
        await vm.LoadCommand.ExecuteAsync(null);
        var item = vm.Courses.Should().ContainSingle().Which.Items.Should().ContainSingle().Which;

        await item.DeleteCommand.ExecuteAsync(null);

        vm.Courses.Should().BeEmpty("a course with nothing downloaded has no dashboard row");
        vm.StorageSummary.Should().Be("Using 0 bytes across 0 courses");
    }

    [Fact]
    public async Task OpenItem_RaisesOpenArtifactRequestedWithEntity()
    {
        var vm = CreateViewModel(out var db, out var downloads);
        await db.UpsertCourseAsync(new Course { Id = "c1", Title = "T" });
        var artifact = new Artifact
        {
            CourseId = "c1", Title = "Doc", SourceUrl = "https://a/d.pdf",
            FileType = ArtifactFileType.Pdf, LocalFilePath = "c1/d.pdf",
            DownloadStatus = DownloadStatus.Completed
        };
        await db.UpsertArtifactAsync(artifact);
        downloads.QueueSuccess("s1", 300, "c1/d.pdf");
        await downloads.DownloadAsync("https://a", "c1", "d.pdf", "s1");
        await vm.LoadCommand.ExecuteAsync(null);
        var item = vm.Courses.Should().ContainSingle().Which.Items.Should().ContainSingle().Which;
        Artifact? opened = null;
        vm.OpenArtifactRequested += a => opened = a;

        item.OpenCommand.Execute(null);

        opened.Should().BeSameAs(artifact, "open carries the entity so the page can push the viewer");
    }

    [Fact]
    public async Task OpenItem_RaisesOpenLectureRequestedWithEntity()
    {
        var vm = CreateViewModel(out var db, out var downloads);
        await db.UpsertCourseAsync(new Course { Id = "c1", Title = "T" });
        var lecture = new Lecture
        {
            CourseId = "c1", Title = "Lec", VideoUrl = "https://a/l.mp4",
            LocalVideoPath = "c1/l.mp4", DownloadStatus = DownloadStatus.Completed
        };
        await db.UpsertLectureAsync(lecture);
        downloads.QueueSuccess("s1", 150, "c1/l.mp4");
        await downloads.DownloadAsync("https://a", "c1", "l.mp4", "s1");
        await vm.LoadCommand.ExecuteAsync(null);
        var item = vm.Courses.Should().ContainSingle().Which.Items.Should().ContainSingle().Which;
        Lecture? opened = null;
        vm.OpenLectureRequested += l => opened = l;

        item.OpenCommand.Execute(null);

        opened.Should().BeSameAs(lecture, "open carries the entity so the page can push the player");
    }
}
