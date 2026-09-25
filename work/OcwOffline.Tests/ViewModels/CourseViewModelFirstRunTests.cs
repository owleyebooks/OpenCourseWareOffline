using OcwOffline.Models;
using OcwOffline.Services;
using OcwOffline.Tests.Fakes;
using OcwOffline.ViewModels;

namespace OcwOffline.Tests.ViewModels;

public class CourseViewModelFirstRunTests
{
    private static CourseViewModel CreateViewModel(
        out FakeOcwScraperService scraper, out InMemoryLastCourseStore lastCourse, out FakeConnectivityService connectivity)
    {
        scraper = new FakeOcwScraperService();
        lastCourse = new InMemoryLastCourseStore();
        connectivity = new FakeConnectivityService();
        return new CourseViewModel(scraper, new FakeCourseDatabase(), new FakeDownloadManager(), lastCourse, connectivity);
    }

    [Fact]
    public void InitialState_FirstRunIntroIsVisible()
    {
        var vm = CreateViewModel(out _, out _, out _);

        vm.IsFirstRunIntroVisible.Should().BeTrue("nothing has been fetched and no error is showing");
    }

    [Fact]
    public async Task GetCourseAsync_Success_HidesFirstRunIntro()
    {
        var vm = CreateViewModel(out var scraper, out _, out _);
        vm.CourseSlug = "c1";
        scraper.QueueResult("c1", new ScrapedCourse
        {
            CourseId = "c1",
            SourceUrl = "https://ocw.mit.edu/courses/c1/download/",
            Title = "Introduction to Algorithms"
        });

        await vm.GetCourseCommand.ExecuteAsync(null);

        vm.IsFirstRunIntroVisible.Should().BeFalse("the fetched course replaces the intro");
    }

    [Fact]
    public async Task GetCourseAsync_Failure_HidesFirstRunIntroBehindTheErrorPanel()
    {
        var vm = CreateViewModel(out var scraper, out _, out _);
        vm.CourseSlug = "c1";
        scraper.QueueFailure("c1", new HttpRequestException("no such host"));

        await vm.GetCourseCommand.ExecuteAsync(null);

        vm.IsFirstRunIntroVisible.Should().BeFalse("the error panel replaces the intro instead of stacking beneath it");
        vm.HasFetchError.Should().BeTrue("the failure shows the error panel");
    }

    [Fact]
    public void InitialState_FirstRunIntroIsActive()
    {
        var vm = CreateViewModel(out _, out _, out _);

        vm.HasCompletedFetch.Should().BeFalse("the tab toggles and lists stay hidden until the first successful fetch");
        vm.FetchedCourseTitle.Should().BeEmpty("no course has been fetched yet");
        vm.HasFetchError.Should().BeFalse("no fetch has been attempted yet");
        vm.Artifacts.Should().BeEmpty("no rows exist before the first fetch");
        vm.Lectures.Should().BeEmpty("no rows exist before the first fetch");
    }

    [Fact]
    public async Task GetCourseAsync_Success_LeavesFirstRunState()
    {
        var vm = CreateViewModel(out var scraper, out _, out _);
        vm.CourseSlug = "c1";
        scraper.QueueResult("c1", new ScrapedCourse
        {
            CourseId = "c1",
            SourceUrl = "https://ocw.mit.edu/courses/c1/download/",
            Title = "Introduction to Algorithms"
        });

        await vm.GetCourseCommand.ExecuteAsync(null);

        vm.HasCompletedFetch.Should().BeTrue("the first successful fetch reveals the tabs and lists");
        vm.FetchedCourseTitle.Should().Be("Introduction to Algorithms", "the success header names the fetched course");
    }

    [Fact]
    public async Task GetCourseAsync_Failure_StaysInFirstRunState()
    {
        var vm = CreateViewModel(out var scraper, out _, out _);
        vm.CourseSlug = "c1";
        scraper.QueueFailure("c1", new HttpRequestException("no such host"));

        await vm.GetCourseCommand.ExecuteAsync(null);

        vm.HasCompletedFetch.Should().BeFalse("a failed fetch does not reveal the tabs and lists");
        vm.HasFetchError.Should().BeTrue("the failure shows the error panel instead");
    }
}
