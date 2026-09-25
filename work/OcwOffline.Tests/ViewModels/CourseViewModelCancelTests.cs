using OcwOffline.Models;
using OcwOffline.Services;
using OcwOffline.Tests.Fakes;
using OcwOffline.ViewModels;

namespace OcwOffline.Tests.ViewModels;

public class CourseViewModelCancelTests
{
    private static CourseViewModel CreateViewModel(
        out FakeCourseDatabase db, out FakeDownloadManager downloads)
    {
        db = new FakeCourseDatabase();
        downloads = new FakeDownloadManager();
        return new CourseViewModel(new FakeOcwScraperService(), db, downloads, new InMemoryLastCourseStore(), new FakeConnectivityService());
    }

    [Fact]
    public async Task CancelArtifactAsync_InProgress_CancelsAndResetsTheRow()
    {
        var vm = CreateViewModel(out var db, out var downloads);
        var artifact = new Artifact
        {
            Id = 1, CourseId = "c1", SourceUrl = "https://x/f.pdf", LocalFilePath = "c1/f.pdf",
            DownloadStatus = DownloadStatus.InProgress, Progress = 0.5, BytesDownloaded = 50
        };
        await db.UpsertArtifactAsync(artifact);
        downloads.QueueSuccess("artifact-1", sizeBytes: 100, relativePath: "c1/f.pdf");
        downloads.SimulatePartialFile("c1/f.pdf", 50);

        await vm.CancelArtifactCommand.ExecuteAsync(artifact);

        downloads.CancelledKeys.Should().Contain("artifact-1", "cancel goes through the manager so the partial file is deleted");
        (await downloads.GetTotalStorageUsedAsync()).Should().Be(0, "the partial file no longer occupies storage");
        artifact.DownloadStatus.Should().Be(DownloadStatus.NotStarted, "cancel returns the row to its initial state");
        artifact.LocalFilePath.Should().BeNull("cancel clears the path");
        artifact.Progress.Should().Be(0, "cancel zeroes progress");
        artifact.BytesDownloaded.Should().Be(0, "cancel zeroes downloaded bytes");
        (await db.GetArtifactsForCourseAsync("c1")).Should().ContainSingle("the reset row is persisted");
    }

    [Fact]
    public async Task CancelArtifactAsync_Paused_CancelsToo()
    {
        var vm = CreateViewModel(out _, out var downloads);
        var artifact = new Artifact { Id = 2, CourseId = "c1", DownloadStatus = DownloadStatus.Paused };

        await vm.CancelArtifactCommand.ExecuteAsync(artifact);

        downloads.CancelledKeys.Should().Contain("artifact-2", "a paused row can also be cancelled");
        Assert.Equal(DownloadStatus.NotStarted, artifact.DownloadStatus);
    }

    [Fact]
    public async Task CancelArtifactAsync_NotStarted_NoOp()
    {
        var vm = CreateViewModel(out _, out var downloads);
        var artifact = new Artifact { Id = 3, CourseId = "c1", DownloadStatus = DownloadStatus.NotStarted };

        await vm.CancelArtifactCommand.ExecuteAsync(artifact);

        downloads.CancelledKeys.Should().BeEmpty("there is nothing to cancel");
        Assert.Equal(DownloadStatus.NotStarted, artifact.DownloadStatus);
    }

    [Fact]
    public async Task CancelLectureAsync_InProgress_CancelsAndResetsTheRow()
    {
        var vm = CreateViewModel(out var db, out var downloads);
        var lecture = new Lecture
        {
            Id = 7, CourseId = "c1", VideoUrl = "https://x/v.mp4", LocalVideoPath = "c1/v.mp4",
            DownloadStatus = DownloadStatus.InProgress, Progress = 0.5, BytesDownloaded = 150
        };
        await db.UpsertLectureAsync(lecture);
        downloads.QueueSuccess("lecture-7", sizeBytes: 300, relativePath: "c1/v.mp4");
        downloads.SimulatePartialFile("c1/v.mp4", 150);

        await vm.CancelLectureCommand.ExecuteAsync(lecture);

        downloads.CancelledKeys.Should().Contain("lecture-7", "cancel goes through the manager so the partial file is deleted");
        (await downloads.GetTotalStorageUsedAsync()).Should().Be(0, "the partial file no longer occupies storage");
        lecture.DownloadStatus.Should().Be(DownloadStatus.NotStarted, "cancel returns the row to its initial state");
        lecture.LocalVideoPath.Should().BeNull("cancel clears the path");
        lecture.Progress.Should().Be(0, "cancel zeroes progress");
        lecture.BytesDownloaded.Should().Be(0, "cancel zeroes downloaded bytes");
        (await db.GetLecturesForCourseAsync("c1")).Should().ContainSingle("the reset row is persisted");
    }

    [Fact]
    public async Task CancelLectureAsync_Completed_NoOp()
    {
        var vm = CreateViewModel(out _, out var downloads);
        var lecture = new Lecture { Id = 8, CourseId = "c1", LocalVideoPath = "c1/v.mp4", DownloadStatus = DownloadStatus.Completed };

        await vm.CancelLectureCommand.ExecuteAsync(lecture);

        downloads.CancelledKeys.Should().BeEmpty("a finished download is not cancelled");
        Assert.Equal(DownloadStatus.Completed, lecture.DownloadStatus);
        Assert.Equal("c1/v.mp4", lecture.LocalVideoPath);
    }
}
