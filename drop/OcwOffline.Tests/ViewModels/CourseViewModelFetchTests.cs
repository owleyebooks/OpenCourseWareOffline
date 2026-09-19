using OcwOffline.Models;
using OcwOffline.Services;
using OcwOffline.Tests.Fakes;
using OcwOffline.ViewModels;

namespace OcwOffline.Tests.ViewModels;

/// <summary>
/// FetchAsync was the only CourseViewModel command without a seam or a
/// test. Unblocked by IOcwScraperService, same shape as v34's
/// IDownloadManager unblock. See AUDIT_TRAIL v36.
/// </summary>
public class CourseViewModelFetchTests
{
    private static CourseViewModel CreateViewModel(
        out FakeOcwScraperService scraper, out FakeCourseDatabase db, out FakeDownloadManager downloads)
    {
        scraper = new FakeOcwScraperService();
        db = new FakeCourseDatabase();
        downloads = new FakeDownloadManager();
        return new CourseViewModel(scraper, db, downloads);
    }

    [Fact]
    public async Task FetchAsync_BlankSlug_NoOp()
    {
        var vm = CreateViewModel(out var scraper, out _, out _);
        vm.CourseSlug = "   ";

        await vm.FetchCommand.ExecuteAsync(null);

        Assert.Empty(scraper.RequestedCourseIds);
        Assert.False(vm.IsBusy);
    }

    [Fact]
    public async Task FetchAsync_AlreadyBusy_NoOp()
    {
        var vm = CreateViewModel(out var scraper, out _, out _);
        vm.CourseSlug = "6-042j-fall-2010";
        vm.IsBusy = true;

        await vm.FetchCommand.ExecuteAsync(null);

        Assert.Empty(scraper.RequestedCourseIds);
    }

    [Fact]
    public async Task FetchAsync_Success_AddsCourseZipArtifactAndLecture()
    {
        var vm = CreateViewModel(out var scraper, out var db, out _);
        vm.CourseSlug = " 6-042j-fall-2010 ";
        scraper.QueueResult("6-042j-fall-2010", new ScrapedCourse
        {
            CourseId = "6-042j-fall-2010",
            SourceUrl = "https://ocw.mit.edu/courses/6-042j-fall-2010/download/",
            Title = "Mathematics for Computer Science",
            ZipArchiveUrl = "https://ocw.mit.edu/courses/6-042j-fall-2010/course.zip",
            Artifacts =
            {
                new ScrapedArtifact { Title = "Syllabus", SourceUrl = "https://ocw.mit.edu/syllabus.pdf", FileType = ArtifactFileType.Pdf, FileSizeBytes = 1000 }
            },
            Lectures =
            {
                new ScrapedLecture { Title = "Lecture 1", VideoUrl = "https://ocw.mit.edu/l1.mp4", FileSizeBytes = 2000 }
            }
        });

        await vm.FetchCommand.ExecuteAsync(null);

        Assert.Equal(new[] { "6-042j-fall-2010" }, scraper.RequestedCourseIds);
        Assert.Equal(2, vm.Artifacts.Count); // course zip + syllabus
        Assert.Contains(vm.Artifacts, a => a.Title == "Download course (full zip)" && a.FileType == ArtifactFileType.Zip);
        Assert.Contains(vm.Artifacts, a => a.Title == "Syllabus" && a.FileSizeBytes == 1000);
        Assert.Single(vm.Lectures);
        Assert.Equal("Lecture 1", vm.Lectures[0].Title);
        Assert.Equal("Found 2 resource(s) and 1 lecture video(s).", vm.StatusText);
        Assert.False(vm.IsBusy);
        Assert.NotNull(await db.GetCourseAsync("6-042j-fall-2010"));
    }

    [Fact]
    public async Task FetchAsync_UnmatchedLabels_AppendsCountToStatusText()
    {
        var vm = CreateViewModel(out var scraper, out _, out _);
        vm.CourseSlug = "c1";
        scraper.QueueResult("c1", new ScrapedCourse
        {
            CourseId = "c1",
            SourceUrl = "https://ocw.mit.edu/courses/c1/download/",
            UnmatchedResourceLabels = { "epub 4 MB", "srt 1 kB" }
        });

        await vm.FetchCommand.ExecuteAsync(null);

        Assert.EndsWith("2 label(s) didn't match the expected format — skipped.", vm.StatusText);
    }

    [Fact]
    public async Task FetchAsync_ScraperThrows_SetsFailedStatusAndClearsBusy()
    {
        var vm = CreateViewModel(out var scraper, out _, out _);
        vm.CourseSlug = "c1";
        scraper.QueueFailure("c1", new HttpRequestException("404"));

        await vm.FetchCommand.ExecuteAsync(null);

        Assert.Equal("Failed: 404", vm.StatusText);
        Assert.False(vm.IsBusy);
        Assert.Empty(vm.Artifacts);
    }

    [Fact]
    public async Task FetchAsync_ReFetch_UpdatesExistingArtifactInPlaceRatherThanDuplicating()
    {
        var vm = CreateViewModel(out var scraper, out var db, out _);
        vm.CourseSlug = "c1";
        var scraped = new ScrapedCourse
        {
            CourseId = "c1",
            SourceUrl = "https://ocw.mit.edu/courses/c1/download/",
            Artifacts = { new ScrapedArtifact { Title = "Notes v1", SourceUrl = "https://ocw.mit.edu/notes.pdf", FileType = ArtifactFileType.Pdf, FileSizeBytes = 100 } }
        };
        scraper.QueueResult("c1", scraped);
        await vm.FetchCommand.ExecuteAsync(null);
        var firstId = vm.Artifacts.Single().Id;

        // Re-fetch: same SourceUrl, updated title — should update in place, not duplicate.
        scraper.QueueResult("c1", new ScrapedCourse
        {
            CourseId = "c1",
            SourceUrl = "https://ocw.mit.edu/courses/c1/download/",
            Artifacts = { new ScrapedArtifact { Title = "Notes v2", SourceUrl = "https://ocw.mit.edu/notes.pdf", FileType = ArtifactFileType.Pdf, FileSizeBytes = 150 } }
        });
        await vm.FetchCommand.ExecuteAsync(null);

        Assert.Single(vm.Artifacts);
        Assert.Equal(firstId, vm.Artifacts[0].Id);
        Assert.Equal("Notes v2", vm.Artifacts[0].Title);
        Assert.Single(await db.GetArtifactsForCourseAsync("c1"));
    }
}
