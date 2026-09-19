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
        return new DownloadsDashboardViewModel(db, downloads);
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
        vm.TotalStorageUsedBytes.Should().Be(300, "the skipped course contributes nothing to the total");
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
    public async Task DeleteCourseAsync_RemovesRowAndUpdatesTotal_LeavesOtherCourseIntact()
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
        vm.TotalStorageUsedBytes.Should().Be(150, "deleting c1 must leave only c2's 150 bytes counted");
        Assert.Null(await db.GetCourseAsync("c1"));
        (await downloads.GetStorageUsedByCourseAsync("c1")).Should().Be(0, "deleting the course must release its downloaded bytes");
    }
}
