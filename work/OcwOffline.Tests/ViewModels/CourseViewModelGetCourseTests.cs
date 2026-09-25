using System.Net;
using OcwOffline.Models;
using OcwOffline.Services;
using OcwOffline.Tests.Fakes;
using OcwOffline.ViewModels;

namespace OcwOffline.Tests.ViewModels;

public class CourseViewModelGetCourseTests
{
    private static CourseViewModel CreateViewModel(
        out FakeOcwScraperService scraper, out FakeCourseDatabase db, out FakeDownloadManager downloads,
        out InMemoryLastCourseStore lastCourse, out FakeConnectivityService connectivity)
    {
        scraper = new FakeOcwScraperService();
        db = new FakeCourseDatabase();
        downloads = new FakeDownloadManager();
        lastCourse = new InMemoryLastCourseStore();
        connectivity = new FakeConnectivityService();
        return new CourseViewModel(scraper, db, downloads, lastCourse, connectivity);
    }

    private static ScrapedCourse CourseResult(string courseId, string title) => new()
    {
        CourseId = courseId,
        SourceUrl = $"https://ocw.mit.edu/courses/{courseId}/download/",
        Title = title,
        ZipArchiveUrl = $"https://ocw.mit.edu/courses/{courseId}/course.zip",
        Artifacts =
        {
            new ScrapedArtifact { Title = "Syllabus", SourceUrl = "https://ocw.mit.edu/syllabus.pdf", FileType = ArtifactFileType.Pdf, FileSizeBytes = 1000 }
        },
        Lectures =
        {
            new ScrapedLecture { Title = "Lecture 1", VideoUrl = "https://ocw.mit.edu/l1.mp4", FileSizeBytes = 2000 }
        }
    };

    [Fact]
    public void CourseSlug_DefaultsToEmpty_NoPrefilledIdentifier()
    {
        var vm = CreateViewModel(out _, out _, out _, out _, out _);

        Assert.Equal(string.Empty, vm.CourseSlug);
    }

    [Fact]
    public async Task GetCourseAsync_BlankSlug_NoOp()
    {
        var vm = CreateViewModel(out var scraper, out _, out _, out _, out _);
        vm.CourseSlug = "   ";

        await vm.GetCourseCommand.ExecuteAsync(null);

        Assert.Empty(scraper.RequestedCourseIds);
        Assert.False(vm.IsBusy);
    }

    [Fact]
    public async Task GetCourseAsync_AlreadyBusy_NoOp()
    {
        var vm = CreateViewModel(out var scraper, out _, out _, out _, out _);
        vm.CourseSlug = "6-042j-fall-2010";
        vm.IsBusy = true;

        await vm.GetCourseCommand.ExecuteAsync(null);

        Assert.Empty(scraper.RequestedCourseIds);
    }

    [Fact]
    public async Task GetCourseAsync_Success_AddsCourseZipArtifactAndLecture()
    {
        var vm = CreateViewModel(out var scraper, out var db, out _, out var lastCourse, out _);
        vm.CourseSlug = " 6-042j-fall-2010 ";
        scraper.QueueResult("6-042j-fall-2010", CourseResult("6-042j-fall-2010", "Mathematics for Computer Science"));

        await vm.GetCourseCommand.ExecuteAsync(null);

        scraper.RequestedCourseIds.Should().Equal(new[] { "6-042j-fall-2010" }, "the slug is trimmed before the scrape request");
        vm.Artifacts.Should().HaveCount(2, "the fetch synthesizes the course zip alongside the scraped syllabus");
        vm.Artifacts.Should().Contain(a => a.Title == "Download course (full zip)" && a.FileType == ArtifactFileType.Zip, "the course zip is added as a zip artifact");
        vm.Artifacts.Should().Contain(a => a.Title == "Syllabus" && a.FileSizeBytes == 1000, "the scraped syllabus keeps its title and size");
        vm.Lectures.Should().ContainSingle("the scraped lecture is added");
        Assert.Equal("Lecture 1", vm.Lectures[0].Title);
        vm.IsBusy.Should().BeFalse("the busy flag clears after a successful fetch");
        (await db.GetCourseAsync("6-042j-fall-2010")).Should().NotBeNull("the fetched course is persisted");
    }

    [Fact]
    public async Task GetCourseAsync_Success_SetsHeaderAndFirstRunStateAndPersistsId()
    {
        var vm = CreateViewModel(out var scraper, out _, out _, out var lastCourse, out _);
        vm.CourseSlug = "c1";
        scraper.QueueResult("c1", CourseResult("c1", "Introduction to Algorithms"));

        await vm.GetCourseCommand.ExecuteAsync(null);

        vm.HasCompletedFetch.Should().BeTrue("the tab toggles and lists appear after the first successful fetch");
        vm.FetchedCourseTitle.Should().Be("Introduction to Algorithms", "the success header names the fetched course");
        vm.HasFetchError.Should().BeFalse("a successful fetch shows no error panel");
        vm.StatusText.Should().BeEmpty("the busy text clears when there is nothing to report");
        lastCourse.GetLastCourseId().Should().Be("c1", "the successful fetch persists the course id for startup restore");
    }

    [Fact]
    public async Task GetCourseAsync_UnmatchedLabels_AppendsCountToStatusText()
    {
        var vm = CreateViewModel(out var scraper, out _, out _, out _, out _);
        vm.CourseSlug = "c1";
        scraper.QueueResult("c1", new ScrapedCourse
        {
            CourseId = "c1",
            SourceUrl = "https://ocw.mit.edu/courses/c1/download/",
            UnmatchedResourceLabels = { "epub 4 MB", "srt 1 kB" }
        });

        await vm.GetCourseCommand.ExecuteAsync(null);

        Assert.EndsWith("2 label(s) didn't match the expected format; skipped.", vm.StatusText);
        vm.HasCompletedFetch.Should().BeTrue("unmatched labels do not fail the fetch");
    }

    [Fact]
    public async Task GetCourseAsync_NetworkFailure_ShowsNetworkErrorPanel()
    {
        var vm = CreateViewModel(out var scraper, out _, out _, out var lastCourse, out _);
        vm.CourseSlug = "c1";
        scraper.QueueFailure("c1", new HttpRequestException("no such host"));

        await vm.GetCourseCommand.ExecuteAsync(null);

        vm.HasFetchError.Should().BeTrue("the fetch failure surfaces as an error panel, not a status line");
        vm.FetchErrorTitle.Should().Be("Couldn't reach ocw.mit.edu");
        vm.FetchErrorDetail.Should().Be("Check your connection and try again.");
        vm.IsNetworkError.Should().BeTrue("a connection failure is classified as a network error");
        vm.IsBusy.Should().BeFalse("the busy flag clears after a failed fetch");
        vm.HasCompletedFetch.Should().BeFalse("a failed fetch leaves the first-run intro in place");
        vm.Artifacts.Should().BeEmpty("a failed fetch shows no rows");
        lastCourse.GetLastCourseId().Should().BeNull("a failed fetch persists nothing");
    }

    [Fact]
    public async Task GetCourseAsync_NotFoundFailure_ShowsAddressErrorPanel()
    {
        var vm = CreateViewModel(out var scraper, out _, out _, out _, out _);
        vm.CourseSlug = "c1";
        scraper.QueueFailure("c1", new HttpRequestException("404", null, HttpStatusCode.NotFound));

        await vm.GetCourseCommand.ExecuteAsync(null);

        vm.HasFetchError.Should().BeTrue("the fetch failure surfaces as an error panel");
        vm.FetchErrorTitle.Should().Be("We couldn't find a course at that address");
        vm.FetchErrorDetail.Should().Be("Double-check the link and try again.");
        vm.IsNetworkError.Should().BeFalse("a bad address is not a network error");
    }

    [Fact]
    public async Task GetCourseAsync_UnexpectedFailure_ShowsGenericErrorPanel()
    {
        var vm = CreateViewModel(out var scraper, out _, out _, out _, out _);
        vm.CourseSlug = "c1";
        scraper.QueueFailure("c1", new InvalidOperationException("boom"));

        await vm.GetCourseCommand.ExecuteAsync(null);

        vm.HasFetchError.Should().BeTrue("the fetch failure surfaces as an error panel");
        vm.FetchErrorTitle.Should().Be("Something went wrong");
        vm.FetchErrorDetail.Should().Be("Please try again later.");
        vm.IsNetworkError.Should().BeFalse("an unexpected failure is not a network error");
    }

    [Fact]
    public async Task RetryFetchAsync_NetworkError_RerunsTheFetch()
    {
        var vm = CreateViewModel(out var scraper, out _, out _, out _, out _);
        vm.CourseSlug = "c1";
        scraper.QueueFailure("c1", new HttpRequestException("no such host"));
        await vm.GetCourseCommand.ExecuteAsync(null);
        scraper.QueueResult("c1", CourseResult("c1", "Introduction to Algorithms"));

        await vm.RetryFetchCommand.ExecuteAsync(null);

        vm.HasFetchError.Should().BeFalse("the retried fetch clears the error panel");
        vm.HasCompletedFetch.Should().BeTrue("the retried fetch completes the first run");
        scraper.RequestedCourseIds.Should().HaveCount(2, "Try again re-runs the fetch for a network error");
    }

    [Fact]
    public async Task RetryFetchAsync_AddressError_RefocusesTheEntryInsteadOfRefetching()
    {
        var vm = CreateViewModel(out var scraper, out _, out _, out _, out _);
        vm.CourseSlug = "c1";
        scraper.QueueFailure("c1", new HttpRequestException("404", null, HttpStatusCode.NotFound));
        await vm.GetCourseCommand.ExecuteAsync(null);
        var refocused = false;
        vm.RefocusCourseEntryRequested += () => refocused = true;

        await vm.RetryFetchCommand.ExecuteAsync(null);

        refocused.Should().BeTrue("an address error refocuses the entry so the link can be fixed");
        scraper.RequestedCourseIds.Should().HaveCount(1, "an address error does not re-run the fetch");
    }

    [Fact]
    public async Task GetCourseAsync_ReFetch_UpdatesExistingArtifactInPlaceRatherThanDuplicating()
    {
        var vm = CreateViewModel(out var scraper, out var db, out _, out _, out _);
        vm.CourseSlug = "c1";
        var scraped = new ScrapedCourse
        {
            CourseId = "c1",
            SourceUrl = "https://ocw.mit.edu/courses/c1/download/",
            Artifacts = { new ScrapedArtifact { Title = "Notes v1", SourceUrl = "https://ocw.mit.edu/notes.pdf", FileType = ArtifactFileType.Pdf, FileSizeBytes = 100 } }
        };
        scraper.QueueResult("c1", scraped);
        await vm.GetCourseCommand.ExecuteAsync(null);
        var firstId = vm.Artifacts.Single().Id;

        scraper.QueueResult("c1", new ScrapedCourse
        {
            CourseId = "c1",
            SourceUrl = "https://ocw.mit.edu/courses/c1/download/",
            Artifacts = { new ScrapedArtifact { Title = "Notes v2", SourceUrl = "https://ocw.mit.edu/notes.pdf", FileType = ArtifactFileType.Pdf, FileSizeBytes = 150 } }
        });
        await vm.GetCourseCommand.ExecuteAsync(null);

        vm.Artifacts.Should().ContainSingle("re-fetching updates the artifact in place instead of duplicating it");
        vm.Artifacts[0].Id.Should().Be(firstId, "the existing artifact keeps its id");
        vm.Artifacts[0].Title.Should().Be("Notes v2", "the re-fetched title overwrites the old one");
        (await db.GetArtifactsForCourseAsync("c1")).Should().ContainSingle("the database gains no duplicate row on re-fetch");
    }
}
