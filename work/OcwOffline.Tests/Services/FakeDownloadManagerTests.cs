using OcwOffline.Services;
using OcwOffline.Tests.Fakes;

namespace OcwOffline.Tests.Services;

public class FakeDownloadManagerTests
{
    [Fact]
    public async Task DownloadAsync_QueuedSuccess_ReturnsSubfolderFileNamePath()
    {
        var downloads = new FakeDownloadManager();
        downloads.QueueSuccess("artifact-1", sizeBytes: 100);

        var path = await downloads.DownloadAsync("https://a/video.mp4", "c1", "video.mp4", "artifact-1");

        Assert.Equal(Path.Combine("c1", "video.mp4"), path);
    }

    [Fact]
    public async Task DownloadAsync_QueuedSuccess_ExplicitRelativePathOverridesDefault()
    {
        var downloads = new FakeDownloadManager();
        downloads.QueueSuccess("artifact-1", sizeBytes: 100, relativePath: "c1/renamed.mp4");

        var path = await downloads.DownloadAsync("https://a/video.mp4", "c1", "video.mp4", "artifact-1");

        Assert.Equal("c1/renamed.mp4", path);
    }

    [Fact]
    public async Task DownloadAsync_QueuedFailure_ThrowsScriptedException()
    {
        var downloads = new FakeDownloadManager();
        downloads.QueueFailure("artifact-1", new HttpRequestException("404"));

        await Assert.ThrowsAsync<HttpRequestException>(
            () => downloads.DownloadAsync("https://a/x", "c1", "x", "artifact-1"));
    }

    [Fact]
    public async Task DownloadAsync_NoQueuedOutcome_ThrowsInvalidOperationException()
    {
        var downloads = new FakeDownloadManager();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => downloads.DownloadAsync("https://a/x", "c1", "x", "unscripted-key"));
    }

    [Fact]
    public async Task DownloadAsync_CancelledToken_ThrowsOperationCanceledException()
    {
        var downloads = new FakeDownloadManager();
        downloads.QueueSuccess("artifact-1", sizeBytes: 100);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => downloads.DownloadAsync("https://a/x", "c1", "x", "artifact-1", cts.Token));
    }

    [Fact]
    public async Task DownloadAsync_Success_RaisesProgressChangedAtFullFraction()
    {
        var downloads = new FakeDownloadManager();
        downloads.QueueSuccess("artifact-1", sizeBytes: 200);
        DownloadProgress? seen = null;
        downloads.ProgressChanged += p => seen = p;

        await downloads.DownloadAsync("https://a/x", "c1", "x", "artifact-1");

        Assert.NotNull(seen);
        Assert.Equal(1.0, seen!.Fraction);
    }

    [Fact]
    public void Pause_MarkedKey_IsReportedByWasPaused()
    {
        var downloads = new FakeDownloadManager();

        downloads.Pause("artifact-1");

        Assert.True(downloads.WasPaused("artifact-1"));
        Assert.False(downloads.WasPaused("artifact-2"));
    }

    [Fact]
    public async Task DownloadAsync_PausedKey_ThrowsOperationCanceledInsteadOfScriptedOutcome()
    {
        var downloads = new FakeDownloadManager();
        downloads.QueueSuccess("artifact-1", sizeBytes: 100);
        downloads.Pause("artifact-1");

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => downloads.DownloadAsync("https://a/x", "c1", "x", "artifact-1"));
    }

    [Fact]
    public async Task DownloadAsync_PausedKey_WasPausedStaysTrueAfterCancellation()
    {
        var downloads = new FakeDownloadManager();
        downloads.QueueSuccess("artifact-1", sizeBytes: 100);
        downloads.Pause("artifact-1");
        await Assert.ThrowsAsync<OperationCanceledException>(
            () => downloads.DownloadAsync("https://a/x", "c1", "x", "artifact-1"));

        Assert.True(downloads.WasPaused("artifact-1"));
    }

    [Fact]
    public async Task DownloadAsync_PausedThenRetried_SecondCallRunsScriptedOutcome()
    {
        var downloads = new FakeDownloadManager();
        downloads.QueueSuccess("artifact-1", sizeBytes: 100, relativePath: "c1/x");
        downloads.Pause("artifact-1");
        await Assert.ThrowsAsync<OperationCanceledException>(
            () => downloads.DownloadAsync("https://a/x", "c1", "x", "artifact-1"));

        var path = await downloads.DownloadAsync("https://a/x", "c1", "x", "artifact-1");

        Assert.Equal("c1/x", path);
    }

    [Fact]
    public async Task DownloadAsync_Success_RaisesProgressChangedTwiceWithFinalAtFullFraction()
    {
        var downloads = new FakeDownloadManager();
        downloads.QueueSuccess("artifact-1", sizeBytes: 200);
        var seen = new List<DownloadProgress>();
        downloads.ProgressChanged += p => seen.Add(p);

        await downloads.DownloadAsync("https://a/x", "c1", "x", "artifact-1");

        Assert.Equal(2, seen.Count);
        Assert.Equal(0.5, seen[0].Fraction);
        Assert.Equal(1.0, seen[1].Fraction);
    }

    [Fact]
    public async Task DownloadAsync_ZeroSizeSuccess_RaisesProgressChangedOnceOnly()
    {
        var downloads = new FakeDownloadManager();
        downloads.QueueSuccess("artifact-1", sizeBytes: 0);
        var seen = new List<DownloadProgress>();
        downloads.ProgressChanged += p => seen.Add(p);

        await downloads.DownloadAsync("https://a/x", "c1", "x", "artifact-1");

        Assert.Single(seen);
    }

    [Fact]
    public async Task GetTotalStorageUsedAsync_SumsAcrossCourses()
    {
        var downloads = new FakeDownloadManager();
        downloads.QueueSuccess("a1", sizeBytes: 100);
        await downloads.DownloadAsync("https://a", "c1", "f1", "a1");
        downloads.QueueSuccess("a2", sizeBytes: 250);
        await downloads.DownloadAsync("https://b", "c2", "f2", "a2");

        var total = await downloads.GetTotalStorageUsedAsync();

        Assert.Equal(350, total);
    }

    [Fact]
    public async Task GetStorageUsedByCourseAsync_ScopedToOneCourse()
    {
        var downloads = new FakeDownloadManager();
        downloads.QueueSuccess("a1", sizeBytes: 100);
        await downloads.DownloadAsync("https://a", "c1", "f1", "a1");
        downloads.QueueSuccess("a2", sizeBytes: 250);
        await downloads.DownloadAsync("https://b", "c2", "f2", "a2");

        Assert.Equal(100, await downloads.GetStorageUsedByCourseAsync("c1"));
        Assert.Equal(250, await downloads.GetStorageUsedByCourseAsync("c2"));
    }

    [Fact]
    public async Task DeleteFile_RemovesOnlyThatFileFromStorageTotal()
    {
        var downloads = new FakeDownloadManager();
        downloads.QueueSuccess("a1", sizeBytes: 100);
        var path1 = await downloads.DownloadAsync("https://a", "c1", "f1", "a1");
        downloads.QueueSuccess("a2", sizeBytes: 250);
        await downloads.DownloadAsync("https://b", "c1", "f2", "a2");

        downloads.DeleteFile(path1);

        Assert.Equal(250, await downloads.GetTotalStorageUsedAsync());
    }

    [Fact]
    public async Task DeleteCourseFolder_RemovesOnlyThatCoursesFiles()
    {
        var downloads = new FakeDownloadManager();
        downloads.QueueSuccess("a1", sizeBytes: 100);
        await downloads.DownloadAsync("https://a", "c1", "f1", "a1");
        downloads.QueueSuccess("a2", sizeBytes: 250);
        await downloads.DownloadAsync("https://b", "c2", "f2", "a2");

        downloads.DeleteCourseFolder("c1");

        Assert.Equal(0, await downloads.GetStorageUsedByCourseAsync("c1"));
        Assert.Equal(250, await downloads.GetStorageUsedByCourseAsync("c2"));
    }

    [Fact]
    public async Task ExtractZipAsync_RecordsDestinationSubfolder()
    {
        var downloads = new FakeDownloadManager();

        await downloads.ExtractZipAsync("/tmp/x.zip", "c1/_extracted_5");

        Assert.Single(downloads.ExtractedZipDestinations);
        Assert.Equal("c1/_extracted_5", downloads.ExtractedZipDestinations[0]);
    }

    [Fact]
    public async Task ExtractZipAsync_QueuedFailure_ThrowsAndDoesNotRecordDestination()
    {
        var downloads = new FakeDownloadManager();
        downloads.QueueExtractFailure("c1/_extracted_5", new InvalidOperationException("bad archive"));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => downloads.ExtractZipAsync("/tmp/x.zip", "c1/_extracted_5"));

        Assert.Empty(downloads.ExtractedZipDestinations);
    }

    [Fact]
    public async Task ExtractZipAsync_QueuedFailure_ConsumedOnUse()
    {
        var downloads = new FakeDownloadManager();
        downloads.QueueExtractFailure("c1/_extracted_5", new InvalidOperationException("bad archive"));
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => downloads.ExtractZipAsync("/tmp/x.zip", "c1/_extracted_5"));

        await downloads.ExtractZipAsync("/tmp/x.zip", "c1/_extracted_5");

        Assert.Single(downloads.ExtractedZipDestinations);
    }

    [Fact]
    public void DeleteExtractedContents_RecordsCourseIdAndArtifactId()
    {
        var downloads = new FakeDownloadManager();

        downloads.DeleteExtractedContents("c1", 5);

        Assert.Single(downloads.DeletedExtractedContents);
        Assert.Equal(Path.Combine("c1", "_extracted_5"), downloads.DeletedExtractedContents[0]);
    }
}
