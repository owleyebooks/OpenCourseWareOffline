using System.Diagnostics;
using System.Net;
using OcwOffline.Models;
using OcwOffline.Services;
using OcwOffline.Tests.Fakes;

namespace OcwOffline.Tests.Services;

/// <summary>
/// Drives the REAL <see cref="DownloadManager"/> (new ctor) against a
/// scripted <see cref="HttpMessageHandler"/> instead of the network:
/// pause, cancel, resume, and zip extraction through the item-level
/// methods, plus transport-level cancel and unknown-key cancel.
/// </summary>
public sealed class DownloadManagerItemTests : IDisposable
{
    private readonly List<string> _tempDirs = new();

    private sealed class TempAppPaths(string root) : IAppPaths
    {
        public string Root { get; } = root;
    }

    // Serves a fixed payload in delayed chunks and honors Range with 206,
    // so pause/resume flows behave like a real resumable server.
    private sealed class ChunkedHandler : HttpMessageHandler
    {
        private readonly byte[] _payload;
        private readonly int _chunkBytes;
        private readonly int _delayMs;

        public ChunkedHandler(byte[] payload, int chunkBytes = 8192, int delayMs = 5)
        {
            _payload = payload;
            _chunkBytes = chunkBytes;
            _delayMs = delayMs;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var from = 0;
            var range = request.Headers.Range?.Ranges.FirstOrDefault();
            if (range is not null)
                from = (int)(range.From ?? 0);

            var response = new HttpResponseMessage(
                from > 0 ? HttpStatusCode.PartialContent : HttpStatusCode.OK);
            var content = new DelayedContent(_payload, from, _chunkBytes, _delayMs);
            if (from > 0)
                content.Headers.ContentRange = new System.Net.Http.Headers.ContentRangeHeaderValue(
                    from, _payload.Length - 1, _payload.Length);
            response.Content = content;
            return Task.FromResult(response);
        }

        private sealed class DelayedContent : HttpContent
        {
            private readonly byte[] _payload;
            private readonly int _offset;
            private readonly int _chunkBytes;
            private readonly int _delayMs;

            public DelayedContent(byte[] payload, int offset, int chunkBytes, int delayMs)
            {
                _payload = payload;
                _offset = offset;
                _chunkBytes = chunkBytes;
                _delayMs = delayMs;
            }

            protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context) =>
                SerializeToStreamAsync(stream, context, CancellationToken.None);

            protected override async Task SerializeToStreamAsync(
                Stream stream, TransportContext? context, CancellationToken cancellationToken)
            {
                var written = 0;
                var length = _payload.Length - _offset;
                while (written < length)
                {
                    await Task.Delay(_delayMs, cancellationToken);
                    var n = Math.Min(_chunkBytes, length - written);
                    await stream.WriteAsync(
                        _payload.AsMemory(_offset + written, n), cancellationToken);
                    written += n;
                }
            }

            protected override bool TryComputeLength(out long length)
            {
                length = _payload.Length - _offset;
                return true;
            }
        }
    }

    private sealed class DownloadScope : IDisposable
    {
        public DownloadScope(byte[] payload, List<string> tempDirs, int chunkBytes = 8192, int delayMs = 5)
        {
            Root = Path.Combine(Path.GetTempPath(), "ocw-dl-it", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Root);
            tempDirs.Add(Root);
            Db = new FakeCourseDatabase();
            Manager = new DownloadManager(
                Db,
                new FakeMainThreadDispatcher(),
                new TempAppPaths(Root),
                new ChunkedHandler(payload, chunkBytes, delayMs));
        }

        public string Root { get; }
        public FakeCourseDatabase Db { get; }
        public DownloadManager Manager { get; }

        public async Task<Artifact> AddArtifactAsync(
            string courseId, string sourceUrl, ArtifactFileType fileType = ArtifactFileType.Other)
        {
            var artifact = new Artifact
            {
                CourseId = courseId,
                Title = "Item",
                SourceUrl = sourceUrl,
                FileType = fileType
            };
            await Db.UpsertArtifactAsync(artifact);
            return artifact;
        }

        public string PartialPath(string subfolder, string fileName) =>
            Path.Combine(Root, subfolder, fileName);

        public async Task WaitForActiveAsync(string key) =>
            await WaitForAsync(
                () => Manager.GetActiveDownloads().Any(a => a.Key == key),
                $"key '{key}' to appear in GetActiveDownloads");

        public void Dispose() { }
    }

    private DownloadScope CreateScope(byte[] payload, int chunkBytes = 8192, int delayMs = 5) =>
        new(payload, _tempDirs, chunkBytes, delayMs);

    private DownloadScope CreateScope(int totalBytes) =>
        CreateScope(FillBytes(totalBytes));

    private static byte[] FillBytes(int totalBytes)
    {
        var payload = new byte[totalBytes];
        new Random(42).NextBytes(payload);
        return payload;
    }

    private static byte[] BuildZip(string entryName, string entryText)
    {
        using var stream = new MemoryStream();
        using (var archive = new System.IO.Compression.ZipArchive(stream, System.IO.Compression.ZipArchiveMode.Create, leaveOpen: true))
        {
            var entry = archive.CreateEntry(entryName);
            using var writer = new StreamWriter(entry.Open());
            writer.Write(entryText);
        }
        return stream.ToArray();
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

    public void Dispose()
    {
        foreach (var dir in _tempDirs)
        {
            try { Directory.Delete(dir, recursive: true); }
            catch (IOException) { /* best effort: a download may still hold the file on a failed test */ }
            catch (UnauthorizedAccessException) { /* best effort */ }
        }
    }

    [Fact]
    public void Cancel_UnknownKey_IsSilentNoOp()
    {
        using var scope = CreateScope(totalBytes: 1000);

        var ex = Record.Exception(() => scope.Manager.Cancel("no-such-key"));

        ex.Should().BeNull("cancel on an unknown key must be a silent no-op");
        scope.Manager.GetAggregate().Should().Be(
            DownloadAggregate.None, "nothing was ever active");
    }

    [Fact]
    public async Task DownloadAsync_CancelMidFlight_ThrowsOperationCanceledAndDeletesPartial()
    {
        using var scope = CreateScope(FillBytes(2_000_000), chunkBytes: 8192, delayMs: 10);
        var partialPath = scope.PartialPath("c1", "f.bin");

        // Plant partial bytes: the manager resumes from them, and Cancel
        // must delete them. (Waiting for the download itself to write
        // partial bytes does not work: ReadAsStreamAsync buffers the body
        // before the file is created, so the file only appears when the
        // download is already done.)
        Directory.CreateDirectory(Path.GetDirectoryName(partialPath)!);
        await File.WriteAllBytesAsync(partialPath, new byte[8192]);

        var task = scope.Manager.DownloadAsync("https://example.com/f.bin", "c1", "f.bin", "k1");
        await scope.WaitForActiveAsync("k1");

        scope.Manager.Cancel("k1");

        // ThrowsAny: the abort can surface as TaskCanceledException
        // (from ReadAsStreamAsync) or OperationCanceledException (from the
        // read loop); both are OperationCanceledException, which is what
        // the item-level callers catch.
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => task);
        File.Exists(partialPath).Should().BeFalse("cancel must delete the partial file");
        scope.Manager.GetAggregate().Should().Be(
            DownloadAggregate.None, "cancel removes the key from the aggregate");
    }

    [Fact]
    public async Task DownloadArtifactAsync_CancelMidFlight_ResetsEntityDeletesPartialAndReturnsNormally()
    {
        using var scope = CreateScope(FillBytes(2_000_000), chunkBytes: 8192, delayMs: 10);
        var artifact = await scope.AddArtifactAsync("c1", "https://example.com/v.mp4");
        var key = $"artifact-{artifact.Id}";
        var partialPath = scope.PartialPath("c1", "v.mp4");

        // Plant partial bytes for the same reason as the transport-level
        // test: Cancel must delete them.
        Directory.CreateDirectory(Path.GetDirectoryName(partialPath)!);
        await File.WriteAllBytesAsync(partialPath, new byte[8192]);

        var downloadTask = scope.Manager.DownloadArtifactAsync(artifact);
        await scope.WaitForActiveAsync(key);

        scope.Manager.Cancel(key);
        var ex = await Record.ExceptionAsync(() => downloadTask);

        ex.Should().BeNull("no exception escapes the item method for cancel");
        artifact.DownloadStatus.Should().Be(DownloadStatus.NotStarted, "cancel resets the row to NotStarted");
        artifact.Progress.Should().Be(0, "cancel zeroes progress");
        artifact.BytesDownloaded.Should().Be(0, "cancel zeroes byte counts");
        artifact.LocalFilePath.Should().BeNull("cancel clears the path");
        File.Exists(partialPath).Should().BeFalse("cancel must delete the partial file");
        (await scope.Db.GetArtifactAsync(artifact.Id))!.DownloadStatus.Should().Be(
            DownloadStatus.NotStarted, "the reset must be persisted");
        scope.Manager.GetAggregate().Should().Be(
            DownloadAggregate.None, "cancel removes the key from the aggregate");
    }

    [Fact]
    public async Task DownloadArtifactAsync_PauseMidFlight_KeepsPartialBytesAndResumesToCompleted()
    {
        using var scope = CreateScope(FillBytes(2_000_000), chunkBytes: 8192, delayMs: 10);
        var artifact = await scope.AddArtifactAsync("c1", "https://example.com/v.mp4");
        var key = $"artifact-{artifact.Id}";
        var partialPath = scope.PartialPath("c1", "v.mp4");

        // Plant partial bytes so the pause-keeps-partial assertion is
        // meaningful (see the cancel tests above for why).
        Directory.CreateDirectory(Path.GetDirectoryName(partialPath)!);
        await File.WriteAllBytesAsync(partialPath, new byte[8192]);

        var downloadTask = scope.Manager.DownloadArtifactAsync(artifact);
        await scope.WaitForActiveAsync(key);

        scope.Manager.Pause(key);
        var ex = await Record.ExceptionAsync(() => downloadTask);

        ex.Should().BeNull("pause returns normally, like cancel");
        artifact.DownloadStatus.Should().Be(DownloadStatus.Paused, "pause surfaces as Paused, not Failed");
        File.Exists(partialPath).Should().BeTrue("pause keeps partial bytes for resume");
        scope.Manager.GetActiveDownloads().Should().ContainSingle(
            "a paused download keeps its snapshot for the dashboard").Which.Key.Should().Be(key);

        await scope.Manager.DownloadArtifactAsync(artifact);

        artifact.DownloadStatus.Should().Be(DownloadStatus.Completed, "resume finishes the download");
        artifact.LocalFilePath.Should().NotBeNull();
        new FileInfo(Path.Combine(scope.Root, artifact.LocalFilePath!)).Length.Should().Be(
            2_000_000, "the resumed file holds all bytes, none duplicated or lost");
        scope.Manager.GetAggregate().Should().Be(
            DownloadAggregate.None, "completion removes the key from the aggregate");
    }

    [Fact]
    public async Task DownloadLectureAsync_Success_SetsCompletedPathAndProgress()
    {
        using var scope = CreateScope(totalBytes: 50_000);
        var lecture = new Lecture { CourseId = "c1", Title = "Lec 1", VideoUrl = "https://example.com/l1.mp4" };
        await scope.Db.UpsertLectureAsync(lecture);

        await scope.Manager.DownloadLectureAsync(lecture);

        lecture.DownloadStatus.Should().Be(DownloadStatus.Completed);
        lecture.LocalVideoPath.Should().Be(Path.Combine("c1", "l1.mp4"));
        lecture.Progress.Should().Be(1);
        (await scope.Db.GetLectureAsync(lecture.Id))!.DownloadStatus.Should().Be(
            DownloadStatus.Completed, "completion must be persisted");
    }

    [Fact]
    public async Task DownloadArtifactAsync_ZipArtifact_ExtractsToPerArtifactSubfolder()
    {
        using var scope = CreateScope(BuildZip("hello.txt", "hello"));
        var artifact = await scope.AddArtifactAsync(
            "c1", "https://example.com/course.zip", ArtifactFileType.Zip);

        await scope.Manager.DownloadArtifactAsync(artifact);

        artifact.DownloadStatus.Should().Be(DownloadStatus.Completed);
        artifact.IsExtracted.Should().BeTrue();
        File.Exists(Path.Combine(scope.Root, "c1", $"_extracted_{artifact.Id}", "hello.txt")).Should().BeTrue(
            "the zip lands in {courseId}/_extracted_{artifactId}");
    }

    [Fact]
    public async Task DownloadArtifactAsync_ZipArtifact_ExtractFailure_KeepsCompletedWithIsExtractedFalse()
    {
        using var scope = CreateScope(new byte[] { 1, 2, 3, 4 });
        var artifact = await scope.AddArtifactAsync(
            "c1", "https://example.com/course.zip", ArtifactFileType.Zip);

        await scope.Manager.DownloadArtifactAsync(artifact);

        artifact.DownloadStatus.Should().Be(
            DownloadStatus.Completed, "a bad archive is not a download failure; re-downloading cannot fix it");
        artifact.IsExtracted.Should().BeFalse();
    }
}
