using OcwOffline.Models;
using OcwOffline.Services;
using OcwOffline.Tests.Fakes;
using OcwOffline.ViewModels;

namespace OcwOffline.Tests.ViewModels;

public class CourseViewModelCommandTests
{
    // OcwScraperService's constructor is parameterless-safe: HttpClient is
    // internal and never called unless ScrapeDownloadPageAsync itself runs.
    private static CourseViewModel CreateViewModel(out FakeCourseDatabase db, out FakeDownloadManager downloads)
    {
        db = new FakeCourseDatabase();
        downloads = new FakeDownloadManager();
        return new CourseViewModel(new OcwScraperService(), db, downloads, new InMemoryLastCourseStore(), new FakeConnectivityService());
    }

    [Fact]
    public async Task RefreshStorageAsync_SetsTotalStorageUsedBytesFromDownloadManager()
    {
        var vm = CreateViewModel(out _, out var downloads);
        downloads.QueueSuccess("seed", sizeBytes: 500);
        await downloads.DownloadAsync("https://a", "c1", "f", "seed");

        await vm.RefreshStorageCommand.ExecuteAsync(null);

        Assert.Equal(500, vm.TotalStorageUsedBytes);
    }

    [Fact]
    public async Task DeleteArtifactAsync_InProgress_LeavesArtifactAndFileUntouched()
    {
        var vm = CreateViewModel(out _, out var downloads);
        downloads.QueueSuccess("seed", 100, "c1/f.pdf");
        await downloads.DownloadAsync("https://a", "c1", "f.pdf", "seed");
        var artifact = new Artifact { CourseId = "c1", LocalFilePath = "c1/f.pdf", DownloadStatus = DownloadStatus.InProgress };

        await vm.DeleteArtifactCommand.ExecuteAsync(artifact);

        Assert.Equal(DownloadStatus.InProgress, artifact.DownloadStatus);
        Assert.Equal(100, await downloads.GetTotalStorageUsedAsync());
    }

    [Fact]
    public async Task DeleteArtifactAsync_Completed_DeletesFileAndResetsFields()
    {
        var vm = CreateViewModel(out var db, out var downloads);
        downloads.QueueSuccess("seed", 100, "c1/f.pdf");
        await downloads.DownloadAsync("https://a", "c1", "f.pdf", "seed");
        var artifact = new Artifact
        {
            CourseId = "c1", LocalFilePath = "c1/f.pdf",
            DownloadStatus = DownloadStatus.Completed, Progress = 1, BytesDownloaded = 100
        };
        await db.UpsertArtifactAsync(artifact);

        await vm.DeleteArtifactCommand.ExecuteAsync(artifact);

        Assert.Equal(DownloadStatus.NotStarted, artifact.DownloadStatus);
        Assert.Null(artifact.LocalFilePath);
        Assert.Equal(0, artifact.Progress);
        Assert.Equal(0, artifact.BytesDownloaded);
        (await downloads.GetTotalStorageUsedAsync()).Should().Be(0, "deleting the artifact releases its downloaded bytes");
    }

    [Fact]
    public async Task DeleteArtifactAsync_ExtractedZip_AlsoDeletesExtractedContents()
    {
        var vm = CreateViewModel(out var db, out var downloads);
        var artifact = new Artifact { Id = 7, CourseId = "c1", IsExtracted = true, DownloadStatus = DownloadStatus.Completed };
        await db.UpsertArtifactAsync(artifact);

        await vm.DeleteArtifactCommand.ExecuteAsync(artifact);

        downloads.DeletedExtractedContents.Should().ContainSingle("deleting an extracted zip removes its extracted contents");
        Assert.Equal(Path.Combine("c1", "_extracted_7"), downloads.DeletedExtractedContents[0]);
        artifact.IsExtracted.Should().BeFalse("the artifact is no longer marked extracted");
    }

    [Fact]
    public async Task DeleteLectureAsync_InProgress_NoOp()
    {
        var vm = CreateViewModel(out _, out _);
        var lecture = new Lecture { CourseId = "c1", LocalVideoPath = "c1/v.mp4", DownloadStatus = DownloadStatus.InProgress };

        await vm.DeleteLectureCommand.ExecuteAsync(lecture);

        Assert.Equal(DownloadStatus.InProgress, lecture.DownloadStatus);
        Assert.Equal("c1/v.mp4", lecture.LocalVideoPath);
    }

    [Fact]
    public async Task DeleteLectureAsync_Completed_DeletesFileAndResetsFields()
    {
        var vm = CreateViewModel(out var db, out var downloads);
        downloads.QueueSuccess("seed", 200, "c1/v.mp4");
        await downloads.DownloadAsync("https://a", "c1", "v.mp4", "seed");
        var lecture = new Lecture
        {
            CourseId = "c1", LocalVideoPath = "c1/v.mp4",
            DownloadStatus = DownloadStatus.Completed, Progress = 1, BytesDownloaded = 200
        };
        await db.UpsertLectureAsync(lecture);

        await vm.DeleteLectureCommand.ExecuteAsync(lecture);

        Assert.Equal(DownloadStatus.NotStarted, lecture.DownloadStatus);
        Assert.Null(lecture.LocalVideoPath);
        (await downloads.GetTotalStorageUsedAsync()).Should().Be(0, "deleting the lecture releases its downloaded bytes");
    }
}
