using System.Diagnostics;
using System.Linq;
using OcwOffline.Models;
using OcwOffline.Services;
using OcwOffline.Tests.Fakes;
using OcwOffline.ViewModels;

namespace OcwOffline.Tests.ViewModels;

public class DownloadsDashboardActiveTests
{
    private static DownloadsDashboardViewModel CreateViewModel(out FakeCourseDatabase db, out FakeDownloadManager downloads)
    {
        db = new FakeCourseDatabase();
        downloads = new FakeDownloadManager();
        return new DownloadsDashboardViewModel(db, downloads, new FakeMainThreadDispatcher(), new FakeAppPaths());
    }

    private static async Task<Artifact> DownloadableArtifactAsync(FakeCourseDatabase db)
    {
        await db.UpsertCourseAsync(new Course { Id = "c1", Title = "Physics" });
        var artifact = new Artifact
        {
            CourseId = "c1", Title = "Lecture notes", SourceUrl = "https://a/notes.pdf",
            FileType = ArtifactFileType.Pdf
        };
        await db.UpsertArtifactAsync(artifact);
        return artifact;
    }

    private static async Task WaitForParkedAsync(FakeDownloadManager downloads, string key)
    {
        var sw = Stopwatch.StartNew();
        while (!downloads.IsParked(key))
        {
            if (sw.ElapsedMilliseconds > 15000)
                throw new TimeoutException($"Timed out waiting for '{key}' to park in flight.");
            await Task.Delay(25);
        }
    }

    private static async Task WaitForAsync(Func<bool> condition, string description, int timeoutMs = 15000)
    {
        var sw = Stopwatch.StartNew();
        while (!condition())
        {
            if (sw.ElapsedMilliseconds > timeoutMs)
                throw new TimeoutException($"Timed out waiting for {description}.");
            await Task.Delay(25);
        }
    }

    [Fact]
    public async Task ActiveDownload_ShowsRowWithJoinedArtifactTitle_AndLeavesOnCompletion()
    {
        var vm = CreateViewModel(out var db, out var downloads);
        var artifact = await DownloadableArtifactAsync(db);
        var key = $"artifact-{artifact.Id}";
        downloads.ScriptDownload(key);

        var start = downloads.DownloadArtifactAsync(artifact);
        await WaitForParkedAsync(downloads, key);

        try
        {
            var row = vm.ActiveDownloads.Should().ContainSingle().Which;
            row.ProgressKey.Should().Be(key);
            row.Title.Should().Be(
                "Lecture notes",
                "active rows carry the artifact title, not a file name");
        }
        finally
        {
            downloads.CompleteScriptedDownload(key, 300, "c1/notes.pdf");
            await start;
        }

        vm.ActiveDownloads.Should().BeEmpty("completion removes the download from the active section");
    }

    [Fact]
    public async Task SimulateProgress_UpdatesActiveRowFraction()
    {
        var vm = CreateViewModel(out var db, out var downloads);
        var artifact = await DownloadableArtifactAsync(db);
        var key = $"artifact-{artifact.Id}";
        downloads.ScriptDownload(key);

        var start = downloads.DownloadArtifactAsync(artifact);
        await WaitForParkedAsync(downloads, key);
        downloads.SimulateProgress(key, 50, 100);

        try
        {
            var row = vm.ActiveDownloads.Should().ContainSingle().Which;
            row.Progress.Should().BeApproximately(
                0.5, 1e-9, "the row mirrors the latest progress snapshot");
        }
        finally
        {
            downloads.CompleteScriptedDownload(key, 100, "c1/notes.pdf");
            await start;
        }
    }

    [Fact]
    public async Task PauseCommand_PausesDownloadAndKeepsRowVisible()
    {
        var vm = CreateViewModel(out var db, out var downloads);
        var artifact = await DownloadableArtifactAsync(db);
        var key = $"artifact-{artifact.Id}";
        downloads.ScriptDownload(key);

        var start = downloads.DownloadArtifactAsync(artifact);
        await WaitForParkedAsync(downloads, key);
        var row = vm.ActiveDownloads.Should().ContainSingle().Which;

        row.PauseCommand.Execute(null);
        await start;

        artifact.DownloadStatus.Should().Be(DownloadStatus.Paused);
        await WaitForAsync(
            () => vm.ActiveDownloads.Count == 1,
            "the paused row to stay visible for resume");
    }

    [Fact]
    public async Task ResumeCommand_RestartsPausedDownload()
    {
        var vm = CreateViewModel(out var db, out var downloads);
        var artifact = await DownloadableArtifactAsync(db);
        var key = $"artifact-{artifact.Id}";
        downloads.ScriptDownload(key);

        var start = downloads.DownloadArtifactAsync(artifact);
        await WaitForParkedAsync(downloads, key);
        downloads.Pause(key);
        await start;
        artifact.DownloadStatus.Should().Be(DownloadStatus.Paused);

        downloads.ScriptDownload(key);
        var row = vm.ActiveDownloads.Should().ContainSingle().Which;
        row.ResumeCommand.Execute(null);
        await WaitForParkedAsync(downloads, key);

        vm.ActiveDownloads.Should().ContainSingle().Which.ProgressKey.Should().Be(
            key, "resuming brings the download back into the active section");
        artifact.DownloadStatus.Should().Be(DownloadStatus.InProgress);

        downloads.CompleteScriptedDownload(key, 300, "c1/notes.pdf");
        await WaitForAsync(
            () => artifact.DownloadStatus == DownloadStatus.Completed,
            "the resumed download to complete");
        vm.ActiveDownloads.Should().BeEmpty();
    }

    [Fact]
    public async Task CancelCommand_RemovesRowAndResetsEntity()
    {
        var vm = CreateViewModel(out var db, out var downloads);
        var artifact = await DownloadableArtifactAsync(db);
        var key = $"artifact-{artifact.Id}";
        downloads.ScriptDownload(key);

        var start = downloads.DownloadArtifactAsync(artifact);
        await WaitForParkedAsync(downloads, key);
        var row = vm.ActiveDownloads.Should().ContainSingle().Which;

        row.CancelCommand.Execute(null);
        await start;

        vm.ActiveDownloads.Should().BeEmpty("cancel removes the row from the active section");
        artifact.DownloadStatus.Should().Be(DownloadStatus.NotStarted);
        downloads.CancelledKeys.Should().Contain(key, "cancel records the key");
    }

    [Fact]
    public async Task TransportLevelSnapshot_ProducesNoRow()
    {
        var vm = CreateViewModel(out var db, out var downloads);
        downloads.ScriptDownload("k1");

        var start = downloads.DownloadAsync("https://a/f.bin", "c1", "f.bin", "k1");
        await WaitForParkedAsync(downloads, "k1");

        try
        {
            vm.ActiveDownloads.Should().BeEmpty(
                "the dashboard only shows item downloads it can join to an artifact or lecture");
        }
        finally
        {
            downloads.CompleteScriptedDownload("k1", 100, "c1/f.bin");
            await start;
        }
    }

    [Fact]
    public async Task AggregateChanged_FiresOnStartProgressAndRemoval()
    {
        var vm = CreateViewModel(out var db, out var downloads);
        var seen = new List<DownloadAggregate>();
        downloads.AggregateChanged += a => seen.Add(a);
        var artifact = await DownloadableArtifactAsync(db);
        var key = $"artifact-{artifact.Id}";

        downloads.ScriptDownload(key);
        var start = downloads.DownloadArtifactAsync(artifact);
        await WaitForParkedAsync(downloads, key);
        downloads.SimulateProgress(key, 50, 100);
        downloads.CompleteScriptedDownload(key, 100, "c1/notes.pdf");
        await start;

        seen.Should().NotBeEmpty("starting a download notifies listeners");
        seen.Should().Contain(
            a => a.ActiveCount == 1,
            "an in-flight download shows as active");
        seen.Last().Should().Be(
            DownloadAggregate.None,
            "completion removes the key and the aggregate goes quiet");
    }
}
