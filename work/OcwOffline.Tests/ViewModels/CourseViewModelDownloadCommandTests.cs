using OcwOffline.Models;
using OcwOffline.Services;
using OcwOffline.Tests.Fakes;
using OcwOffline.ViewModels;

namespace OcwOffline.Tests.ViewModels;

public class CourseViewModelDownloadCommandTests
{
    private static CourseViewModel CreateViewModel(
        out FakeCourseDatabase db, out FakeDownloadManager downloads,
        out InMemoryLastCourseStore lastCourse, out FakeConnectivityService connectivity)
    {
        db = new FakeCourseDatabase();
        downloads = new FakeDownloadManager();
        lastCourse = new InMemoryLastCourseStore();
        connectivity = new FakeConnectivityService();
        return new CourseViewModel(new FakeOcwScraperService(), db, downloads, lastCourse, connectivity);
    }

    [Fact]
    public async Task PrimaryArtifactActionAsync_NotStarted_Success_CompletesViaItemApi()
    {
        var vm = CreateViewModel(out _, out var downloads, out _, out _);
        var artifact = new Artifact { Id = 2, CourseId = "c1", SourceUrl = "https://x/notes.pdf", FileType = ArtifactFileType.Pdf };
        downloads.QueueSuccess("artifact-2", sizeBytes: 100, relativePath: "c1/notes.pdf");

        await vm.PrimaryArtifactActionCommand.ExecuteAsync(artifact);

        Assert.Equal(DownloadStatus.Completed, artifact.DownloadStatus);
        Assert.Equal("c1/notes.pdf", artifact.LocalFilePath);
        Assert.Equal(1, artifact.Progress);
        artifact.IsExtracted.Should().BeFalse("a non-zip download is not marked extracted");
    }

    [Fact]
    public async Task PrimaryArtifactActionAsync_ZipType_ExtractsViaItemApi()
    {
        var vm = CreateViewModel(out _, out var downloads, out _, out _);
        var artifact = new Artifact { Id = 3, CourseId = "c1", SourceUrl = "https://x/all.zip", FileType = ArtifactFileType.Zip };
        downloads.QueueSuccess("artifact-3", sizeBytes: 200, relativePath: "c1/all.zip");

        await vm.PrimaryArtifactActionCommand.ExecuteAsync(artifact);

        Assert.Equal(DownloadStatus.Completed, artifact.DownloadStatus);
        artifact.IsExtracted.Should().BeTrue("a zip download is marked extracted");
        downloads.ExtractedZipDestinations.Should().ContainSingle("the zip is extracted exactly once");
        Assert.Equal(Path.Combine("c1", "_extracted_3"), downloads.ExtractedZipDestinations[0]);
    }

    [Fact]
    public async Task PrimaryArtifactActionAsync_Failed_RetriesTheDownload()
    {
        var vm = CreateViewModel(out _, out var downloads, out _, out _);
        var artifact = new Artifact { Id = 4, CourseId = "c1", SourceUrl = "https://x/f.pdf", DownloadStatus = DownloadStatus.Failed };
        downloads.QueueSuccess("artifact-4", sizeBytes: 100, relativePath: "c1/f.pdf");

        await vm.PrimaryArtifactActionCommand.ExecuteAsync(artifact);

        Assert.Equal(DownloadStatus.Completed, artifact.DownloadStatus);
    }

    [Fact]
    public async Task PrimaryArtifactActionAsync_ScriptedFailure_SetsFailedAndStatusText()
    {
        var vm = CreateViewModel(out _, out var downloads, out _, out _);
        var artifact = new Artifact { Id = 5, CourseId = "c1", SourceUrl = "https://x/f.pdf", Title = "Notes" };
        downloads.QueueFailure("artifact-5", new IOException("disk full"));

        await vm.PrimaryArtifactActionCommand.ExecuteAsync(artifact);

        Assert.Equal(DownloadStatus.Failed, artifact.DownloadStatus);
        Assert.Contains("Notes", vm.StatusText);
        Assert.Contains("disk full", vm.StatusText);
    }

    [Fact]
    public async Task PrimaryArtifactActionAsync_Stalled_MessagesTheStall()
    {
        var vm = CreateViewModel(out _, out var downloads, out _, out _);
        var artifact = new Artifact { Id = 6, CourseId = "c1", SourceUrl = "https://x/f.pdf", Title = "Notes" };
        downloads.QueueFailure("artifact-6", new DownloadStalledException("no bytes received for 30s"));

        await vm.PrimaryArtifactActionCommand.ExecuteAsync(artifact);

        vm.StatusText.Should().Contain("stalled", "a watchdog stall is messaged distinctly from a plain failure");
        Assert.Contains("Notes", vm.StatusText);
    }

    [Fact]
    public void PrimaryArtifactAction_InProgress_PausesTheDownload()
    {
        var vm = CreateViewModel(out _, out var downloads, out _, out _);
        var artifact = new Artifact { Id = 9, DownloadStatus = DownloadStatus.InProgress };

        vm.PrimaryArtifactActionCommand.Execute(artifact);

        Assert.True(downloads.WasPaused("artifact-9"));
        Assert.Equal(DownloadStatus.InProgress, artifact.DownloadStatus);
    }

    [Fact]
    public async Task PrimaryArtifactActionAsync_Paused_ResumesFromPartialBytes()
    {
        var vm = CreateViewModel(out _, out var downloads, out _, out _);
        var artifact = new Artifact { Id = 10, CourseId = "c1", SourceUrl = "https://x/f.pdf", DownloadStatus = DownloadStatus.Paused, BytesDownloaded = 50 };
        downloads.QueueSuccess("artifact-10", sizeBytes: 100, relativePath: "c1/f.pdf");

        await vm.PrimaryArtifactActionCommand.ExecuteAsync(artifact);

        Assert.Equal(DownloadStatus.Completed, artifact.DownloadStatus);
    }

    [Fact]
    public async Task PrimaryArtifactActionAsync_PauseQueuedBeforeStart_SetsPausedViaRealCancellationPath()
    {
        var vm = CreateViewModel(out _, out var downloads, out _, out _);
        var artifact = new Artifact { Id = 13, CourseId = "c1", SourceUrl = "https://x/f.pdf" };
        downloads.QueueSuccess("artifact-13", sizeBytes: 100);
        downloads.Pause("artifact-13");

        await vm.PrimaryArtifactActionCommand.ExecuteAsync(artifact);

        Assert.Equal(DownloadStatus.Paused, artifact.DownloadStatus);
    }

    [Fact]
    public void PrimaryArtifactAction_Completed_RaisesOpenArtifactRequestedWithPayload()
    {
        var vm = CreateViewModel(out _, out _, out _, out _);
        var artifact = new Artifact { Id = 14, DownloadStatus = DownloadStatus.Completed };
        Artifact? opened = null;
        vm.OpenArtifactRequested += a => opened = a;

        vm.PrimaryArtifactActionCommand.Execute(artifact);

        opened.Should().BeSameAs(artifact, "tapping a downloaded artifact hands it to the page for viewing");
    }

    [Fact]
    public async Task PrimaryLectureActionAsync_BlankVideoUrl_SkipsAndSetsStatusText()
    {
        var vm = CreateViewModel(out _, out var downloads, out _, out _);
        var lecture = new Lecture { Id = 1, CourseId = "c1", Title = "Lecture 1", VideoUrl = "" };

        await vm.PrimaryLectureActionCommand.ExecuteAsync(lecture);

        Assert.Equal(DownloadStatus.NotStarted, lecture.DownloadStatus);
        Assert.Contains("Lecture 1", vm.StatusText);
        Assert.Equal(0, await downloads.GetTotalStorageUsedAsync());
    }

    [Fact]
    public async Task PrimaryLectureActionAsync_NotStarted_Success_CompletesViaItemApi()
    {
        var vm = CreateViewModel(out _, out var downloads, out _, out _);
        var lecture = new Lecture { Id = 3, CourseId = "c1", VideoUrl = "https://x/v.mp4" };
        downloads.QueueSuccess("lecture-3", sizeBytes: 300, relativePath: "c1/v.mp4");

        await vm.PrimaryLectureActionCommand.ExecuteAsync(lecture);

        Assert.Equal(DownloadStatus.Completed, lecture.DownloadStatus);
        Assert.Equal("c1/v.mp4", lecture.LocalVideoPath);
        Assert.Equal(1, lecture.Progress);
    }

    [Fact]
    public async Task PrimaryLectureActionAsync_ScriptedFailure_SetsFailedAndStatusText()
    {
        var vm = CreateViewModel(out _, out var downloads, out _, out _);
        var lecture = new Lecture { Id = 4, CourseId = "c1", VideoUrl = "https://x/v.mp4", Title = "Lecture 4" };
        downloads.QueueFailure("lecture-4", new IOException("network reset"));

        await vm.PrimaryLectureActionCommand.ExecuteAsync(lecture);

        Assert.Equal(DownloadStatus.Failed, lecture.DownloadStatus);
        Assert.Contains("Lecture 4", vm.StatusText);
    }

    [Fact]
    public void PrimaryLectureAction_InProgress_PausesTheDownload()
    {
        var vm = CreateViewModel(out _, out var downloads, out _, out _);
        var lecture = new Lecture { Id = 11, DownloadStatus = DownloadStatus.InProgress };

        vm.PrimaryLectureActionCommand.Execute(lecture);

        Assert.True(downloads.WasPaused("lecture-11"));
    }

    [Fact]
    public async Task PrimaryLectureActionAsync_Paused_ResumesTheDownload()
    {
        var vm = CreateViewModel(out _, out var downloads, out _, out _);
        var lecture = new Lecture { Id = 12, CourseId = "c1", VideoUrl = "https://x/v.mp4", DownloadStatus = DownloadStatus.Paused };
        downloads.QueueSuccess("lecture-12", sizeBytes: 300, relativePath: "c1/v.mp4");

        await vm.PrimaryLectureActionCommand.ExecuteAsync(lecture);

        Assert.Equal(DownloadStatus.Completed, lecture.DownloadStatus);
    }

    [Fact]
    public void PrimaryLectureAction_Completed_RaisesOpenLectureRequestedWithPayload()
    {
        var vm = CreateViewModel(out _, out _, out _, out _);
        var lecture = new Lecture { Id = 15, DownloadStatus = DownloadStatus.Completed };
        Lecture? opened = null;
        vm.OpenLectureRequested += l => opened = l;

        vm.PrimaryLectureActionCommand.Execute(lecture);

        opened.Should().BeSameAs(lecture, "tapping a downloaded lecture hands it to the page for playback");
    }
}
