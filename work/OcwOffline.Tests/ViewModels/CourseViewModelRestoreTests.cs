using OcwOffline.Models;
using OcwOffline.Services;
using OcwOffline.Tests.Fakes;
using OcwOffline.ViewModels;

namespace OcwOffline.Tests.ViewModels;

public class CourseViewModelRestoreTests
{
    private static CourseViewModel CreateViewModel(
        out FakeOcwScraperService scraper, out InMemoryLastCourseStore lastCourse, out FakeConnectivityService connectivity)
    {
        scraper = new FakeOcwScraperService();
        lastCourse = new InMemoryLastCourseStore();
        connectivity = new FakeConnectivityService();
        return new CourseViewModel(scraper, new FakeCourseDatabase(), new FakeDownloadManager(), lastCourse, connectivity);
    }

    private static ScrapedCourse CourseResult(string courseId) => new()
    {
        CourseId = courseId,
        SourceUrl = $"https://ocw.mit.edu/courses/{courseId}/download/",
        Title = "Some Course"
    };

    [Fact]
    public async Task RestoreLastCourseAsync_OfflineWithStoredId_AttemptsNothingAndShowsNoError()
    {
        var vm = CreateViewModel(out var scraper, out var lastCourse, out var connectivity);
        lastCourse.SetLastCourseId("c1");
        connectivity.SetConnected(false);

        await vm.RestoreLastCourseAsync();

        scraper.RequestedCourseIds.Should().BeEmpty("an offline restore never attempts a fetch");
        vm.HasFetchError.Should().BeFalse("an offline restore never shows an error screen");
        vm.HasCompletedFetch.Should().BeFalse("an offline restore leaves the first-run intro in place");
        vm.CourseSlug.Should().BeEmpty("an offline restore does not touch the entry field");
    }

    [Fact]
    public async Task RestoreLastCourseAsync_OnlineWithStoredId_FetchesTheStoredCourse()
    {
        var vm = CreateViewModel(out var scraper, out var lastCourse, out var connectivity);
        lastCourse.SetLastCourseId("c1");
        connectivity.SetConnected(true);
        scraper.QueueResult("c1", CourseResult("c1"));

        await vm.RestoreLastCourseAsync();

        vm.CourseSlug.Should().Be("c1", "the stored id lands in the entry field before the fetch");
        scraper.RequestedCourseIds.Should().Equal(new[] { "c1" }, "an online restore fetches the stored course");
        vm.HasCompletedFetch.Should().BeTrue("the restored fetch completes the first run");
    }

    [Fact]
    public async Task RestoreLastCourseAsync_OnlineWithNothingStored_AttemptsNothing()
    {
        var vm = CreateViewModel(out var scraper, out _, out var connectivity);
        connectivity.SetConnected(true);

        await vm.RestoreLastCourseAsync();

        scraper.RequestedCourseIds.Should().BeEmpty("there is nothing to restore");
        vm.HasCompletedFetch.Should().BeFalse("the first-run intro stays in place");
    }

    [Fact]
    public async Task RestoreLastCourseAsync_CalledTwice_FetchesAtMostOnce()
    {
        var vm = CreateViewModel(out var scraper, out var lastCourse, out var connectivity);
        lastCourse.SetLastCourseId("c1");
        connectivity.SetConnected(true);
        scraper.QueueResult("c1", CourseResult("c1"));

        await vm.RestoreLastCourseAsync();
        await vm.RestoreLastCourseAsync();

        scraper.RequestedCourseIds.Should().ContainSingle("the restore is guarded to run once per view model lifetime");
    }

    [Fact]
    public async Task GetCourseAsync_Success_PersistsIdForStartupRestore()
    {
        var vm = CreateViewModel(out var scraper, out var lastCourse, out _);
        vm.CourseSlug = "c1";
        scraper.QueueResult("c1", CourseResult("c1"));

        await vm.GetCourseCommand.ExecuteAsync(null);

        lastCourse.GetLastCourseId().Should().Be("c1", "every successful fetch refreshes the stored id");
    }
}
