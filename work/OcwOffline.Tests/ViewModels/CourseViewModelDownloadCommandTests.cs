using OcwOffline.Models;
using OcwOffline.Services;
using OcwOffline.Tests.Fakes;
using OcwOffline.ViewModels;

namespace OcwOffline.Tests.ViewModels;

public class CourseViewModelDownloadCommandTests
{
    // Same parameterless-safe reasoning as CourseViewModelCommandTests:
    // OcwScraperService's HttpClient is never touched by these commands.
    private static CourseViewModel CreateViewModel(out FakeDownloadManager downloads)
    {
        downloads = new FakeDownloadManager();
        return new CourseViewModel(new OcwScraperService(), new FakeCourseDatabase(), downloads, new FakeMainThreadDispatcher(), new FakeAppPaths());
    }

    [Fact]
    public async Task DownloadArtifactAsync_InProgress_NoOp()
    {
        var vm = CreateViewModel(out var downloads);
        var artifact = new Artifact { Id = 1, CourseId = "c1", SourceUrl = "https://x/f.pdf", DownloadStatus = DownloadStatus.InProgress };

        await vm.DownloadArtifactCommand.ExecuteAsync(artifact);

        Assert.Equal(DownloadStatus.InProgress, artifact.DownloadStatus);
        Assert.Equal(0, await downloads.GetTotalStorageUsedAsync());
    }

    [Fact]
    public async Task DownloadArtifactAsync_Success_NonZip_SetsCompletedAndLocalFilePath()
    {
        var vm = CreateViewModel(out var downloads);
        var artifact = new Artifact { Id = 2, CourseId = "c1", SourceUrl = "https://x/notes.pdf", FileType = ArtifactFileType.Pdf };
        downloads.QueueSuccess("artifact-2", sizeBytes: 100, relativePath: "c1/notes.pdf");

        await vm.DownloadArtifactCommand.ExecuteAsync(artifact);

        Assert.Equal(DownloadStatus.Completed, artifact.DownloadStatus);
        Assert.Equal("c1/notes.pdf", artifact.LocalFilePath);
        Assert.Equal(1, artifact.Progress);
        artifact.IsExtracted.Should().BeFalse("a non-zip download is not marked extracted");
    }

    [Fact]
    public async Task DownloadArtifactAsync_ZipType_ExtractsAndSetsIsExtracted()
    {
        var vm = CreateViewModel(out var downloads);
        var artifact = new Artifact { Id = 3, CourseId = "c1", SourceUrl = "https://x/all.zip", FileType = ArtifactFileType.Zip };
        downloads.QueueSuccess("artifact-3", sizeBytes: 200, relativePath: "c1/all.zip");

        await vm.DownloadArtifactCommand.ExecuteAsync(artifact);

        Assert.Equal(DownloadStatus.Completed, artifact.DownloadStatus);
        artifact.IsExtracted.Should().BeTrue("a zip download is marked extracted");
        downloads.ExtractedZipDestinations.Should().ContainSingle("the zip is extracted exactly once");
        Assert.Equal(Path.Combine("c1", "_extracted_3"), downloads.ExtractedZipDestinations[0]);
    }

    [Fact]
    public async Task DownloadArtifactAsync_ZipType_ExtractionFails_StaysCompletedNotExtracted()
    {
        var vm = CreateViewModel(out var downloads);
        var artifact = new Artifact { Id = 6, CourseId = "c1", SourceUrl = "https://x/all.zip", FileType = ArtifactFileType.Zip };
        downloads.QueueSuccess("artifact-6", sizeBytes: 200, relativePath: "c1/all.zip");
        downloads.QueueExtractFailure(Path.Combine("c1", "_extracted_6"), new InvalidOperationException("bad archive"));

        await vm.DownloadArtifactCommand.ExecuteAsync(artifact);

        Assert.Equal(DownloadStatus.Completed, artifact.DownloadStatus);
        Assert.False(artifact.IsExtracted);
        Assert.Equal("c1/all.zip", artifact.LocalFilePath);
    }

    [Fact]
    public async Task DownloadArtifactAsync_ScriptedFailure_SetsFailedAndStatusText()
    {
        var vm = CreateViewModel(out var downloads);
        var artifact = new Artifact { Id = 4, CourseId = "c1", SourceUrl = "https://x/f.pdf", Title = "Notes" };
        downloads.QueueFailure("artifact-4", new IOException("disk full"));

        await vm.DownloadArtifactCommand.ExecuteAsync(artifact);

        Assert.Equal(DownloadStatus.Failed, artifact.DownloadStatus);
        Assert.Contains("Notes", vm.StatusText);
        Assert.Contains("disk full", vm.StatusText);
    }

    [Fact]
    public async Task DownloadArtifactAsync_Cancelled_SetsPaused()
    {
        var vm = CreateViewModel(out var downloads);
        var artifact = new Artifact { Id = 5, CourseId = "c1", SourceUrl = "https://x/f.pdf" };
        downloads.QueueFailure("artifact-5", new OperationCanceledException());

        await vm.DownloadArtifactCommand.ExecuteAsync(artifact);

        Assert.Equal(DownloadStatus.Paused, artifact.DownloadStatus);
    }

    [Fact]
    public async Task DownloadLectureAsync_BlankVideoUrl_SkipsAndSetsStatusText()
    {
        var vm = CreateViewModel(out var downloads);
        var lecture = new Lecture { Id = 1, CourseId = "c1", Title = "Lecture 1", VideoUrl = "" };

        await vm.DownloadLectureCommand.ExecuteAsync(lecture);

        Assert.Equal(DownloadStatus.NotStarted, lecture.DownloadStatus);
        Assert.Contains("Lecture 1", vm.StatusText);
        Assert.Equal(0, await downloads.GetTotalStorageUsedAsync());
    }

    [Fact]
    public async Task DownloadLectureAsync_InProgress_NoOp()
    {
        var vm = CreateViewModel(out _);
        var lecture = new Lecture { Id = 2, CourseId = "c1", VideoUrl = "https://x/v.mp4", DownloadStatus = DownloadStatus.InProgress };

        await vm.DownloadLectureCommand.ExecuteAsync(lecture);

        Assert.Equal(DownloadStatus.InProgress, lecture.DownloadStatus);
    }

    [Fact]
    public async Task DownloadLectureAsync_Success_SetsCompletedAndLocalVideoPath()
    {
        var vm = CreateViewModel(out var downloads);
        var lecture = new Lecture { Id = 3, CourseId = "c1", VideoUrl = "https://x/v.mp4" };
        downloads.QueueSuccess("lecture-3", sizeBytes: 300, relativePath: "c1/v.mp4");

        await vm.DownloadLectureCommand.ExecuteAsync(lecture);

        Assert.Equal(DownloadStatus.Completed, lecture.DownloadStatus);
        Assert.Equal("c1/v.mp4", lecture.LocalVideoPath);
        Assert.Equal(1, lecture.Progress);
    }

    [Fact]
    public async Task DownloadLectureAsync_ScriptedFailure_SetsFailedAndStatusText()
    {
        var vm = CreateViewModel(out var downloads);
        var lecture = new Lecture { Id = 4, CourseId = "c1", VideoUrl = "https://x/v.mp4", Title = "Lecture 4" };
        downloads.QueueFailure("lecture-4", new IOException("network reset"));

        await vm.DownloadLectureCommand.ExecuteAsync(lecture);

        Assert.Equal(DownloadStatus.Failed, lecture.DownloadStatus);
        Assert.Contains("Lecture 4", vm.StatusText);
    }

    [Fact]
    public async Task DownloadArtifactAsync_DownloadManagerPausedThisKey_SetsPausedViaRealCancellationPath()
    {
        // Exercises the fake's own cancellation path (fixed this round),
        // not a hand-scripted OperationCanceledException like the
        // Cancelled test above, which covers the ViewModel's catch block.
        var vm = CreateViewModel(out var downloads);
        var artifact = new Artifact { Id = 13, CourseId = "c1", SourceUrl = "https://x/f.pdf" };
        downloads.QueueSuccess("artifact-13", sizeBytes: 100);
        downloads.Pause("artifact-13");

        await vm.DownloadArtifactCommand.ExecuteAsync(artifact);

        Assert.Equal(DownloadStatus.Paused, artifact.DownloadStatus);
    }

    [Fact]
    public void PauseArtifact_InProgress_CallsPauseWithArtifactKey()
    {
        var vm = CreateViewModel(out var downloads);
        var artifact = new Artifact { Id = 9, DownloadStatus = DownloadStatus.InProgress };

        vm.PauseArtifactCommand.Execute(artifact);

        Assert.True(downloads.WasPaused("artifact-9"));
    }

    [Fact]
    public void PauseArtifact_NotInProgress_NoOp()
    {
        var vm = CreateViewModel(out var downloads);
        var artifact = new Artifact { Id = 10, DownloadStatus = DownloadStatus.Completed };

        vm.PauseArtifactCommand.Execute(artifact);

        Assert.False(downloads.WasPaused("artifact-10"));
    }

    [Fact]
    public void PauseLecture_InProgress_CallsPauseWithLectureKey()
    {
        var vm = CreateViewModel(out var downloads);
        var lecture = new Lecture { Id = 11, DownloadStatus = DownloadStatus.InProgress };

        vm.PauseLectureCommand.Execute(lecture);

        Assert.True(downloads.WasPaused("lecture-11"));
    }

    [Fact]
    public void PauseLecture_NotInProgress_NoOp()
    {
        var vm = CreateViewModel(out var downloads);
        var lecture = new Lecture { Id = 12, DownloadStatus = DownloadStatus.NotStarted };

        vm.PauseLectureCommand.Execute(lecture);

        Assert.False(downloads.WasPaused("lecture-12"));
    }
}
