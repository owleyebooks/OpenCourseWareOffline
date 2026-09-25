using Microsoft.Data.Sqlite;
using OcwOffline.Models;
using OcwOffline.Services;

namespace OcwOffline.Tests.Services;

/// <summary>
/// Integration tests for the REAL <see cref="CourseDatabase"/> (sqlite-net-pcl)
/// against a temp-file SQLite database. Each test gets its own temp directory
/// (parallel-safe, no cross-talk); the database under test is driven through
/// <see cref="ICourseDatabase"/> and independently verified through
/// Microsoft.Data.Sqlite, so a passing test proves the rows and schema actually
/// land in the SQLite file, not just that one ORM agrees with itself.
/// </summary>
public sealed class CourseDatabaseIntegrationTests : IDisposable
{
    private readonly List<string> _tempDirs = new();

    private sealed class TempAppPaths(string root) : IAppPaths
    {
        public string Root { get; } = root;
    }

    private CourseDatabase CreateDatabase(out string dbFile)
    {
        var dir = Path.Combine(Path.GetTempPath(), "ocw-sqlite-it", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        _tempDirs.Add(dir);
        dbFile = Path.Combine(dir, "ocw_offline.db3");
        return new CourseDatabase(new TempAppPaths(dir));
    }

    private CourseDatabase CreateDatabase() => CreateDatabase(out _);

    private static List<string> ReadTableNames(string dbFile)
    {
        using var connection = new SqliteConnection($"Data Source={dbFile}");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT name FROM sqlite_master WHERE type = 'table' ORDER BY name;";
        using var reader = command.ExecuteReader();
        var names = new List<string>();
        while (reader.Read())
            names.Add(reader.GetString(0));
        return names;
    }

    private static int CountRows(string dbFile, string table, string? where = null)
    {
        using var connection = new SqliteConnection($"Data Source={dbFile}");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = $"SELECT COUNT(*) FROM {table}" + (where is null ? ";" : $" WHERE {where};");
        return Convert.ToInt32(command.ExecuteScalar());
    }

    public void Dispose()
    {
        foreach (var dir in _tempDirs)
        {
            try { Directory.Delete(dir, recursive: true); }
            catch (IOException) { /* best effort: locked file on a failed test */ }
            catch (UnauthorizedAccessException) { /* best effort */ }
        }
    }

    // ---- Schema creation ----

    [Fact]
    public async Task FirstUse_CreatesDatabaseFileWithExpectedTables()
    {
        var db = CreateDatabase(out var dbFile);

        await db.GetAllCoursesAsync();

        Assert.True(File.Exists(dbFile));
        var tables = ReadTableNames(dbFile);
        Assert.Contains("Course", tables);
        Assert.Contains("Artifact", tables);
        Assert.Contains("Lecture", tables);
    }

    [Fact]
    public async Task FirstUse_CreatesCourseIdIndexes()
    {
        var db = CreateDatabase(out var dbFile);

        await db.GetAllCoursesAsync();

        // The [Indexed] CourseId columns on Artifact/Lecture must materialize
        // as real SQLite indexes. The dashboard queries filter on them.
        using var connection = new SqliteConnection($"Data Source={dbFile}");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT tbl_name FROM sqlite_master WHERE type = 'index' AND tbl_name IN ('Artifact','Lecture');";
        using var reader = command.ExecuteReader();
        var indexed = new List<string>();
        while (reader.Read())
            indexed.Add(reader.GetString(0));
        Assert.Contains("Artifact", indexed);
        Assert.Contains("Lecture", indexed);
    }

    [Fact]
    public async Task ConcurrentFirstAccess_AllCallersGetUsableConnection()
    {
        // Races the double-checked schema-creation guard in GetConnectionAsync:
        // without it, a caller could observe a connection with no tables yet.
        var db = CreateDatabase();

        var results = await Task.WhenAll(
            Enumerable.Range(0, 8).Select(_ => db.GetAllCoursesAsync()));

        Assert.All(results, r => Assert.Empty(r));
    }

    // ---- Courses ----

    [Fact]
    public async Task UpsertCourseAsync_NewCourse_PersistsAllColumnsToSqliteFile()
    {
        var db = CreateDatabase(out var dbFile);
        var scraped = new DateTime(2026, 9, 18, 12, 0, 0, DateTimeKind.Utc);

        await db.UpsertCourseAsync(new Course
        {
            Id = "hst-508-genomics-fall-2002",
            Title = "Genomics and Computational Biology",
            Department = "Health Sciences and Technology",
            Url = "https://ocw.mit.edu/hst-508",
            IsDownloaded = true,
            Term = "Fall 2002",
            LastScrapedUtc = scraped,
        });

        // Independent read through Microsoft.Data.Sqlite: raw column values.
        using var connection = new SqliteConnection($"Data Source={dbFile}");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT Id, Title, Department, Url, IsDownloaded, Term FROM Course;";
        using var reader = command.ExecuteReader();
        Assert.True(reader.Read());
        Assert.Equal("hst-508-genomics-fall-2002", reader.GetString(0));
        Assert.Equal("Genomics and Computational Biology", reader.GetString(1));
        Assert.Equal("Health Sciences and Technology", reader.GetString(2));
        Assert.Equal("https://ocw.mit.edu/hst-508", reader.GetString(3));
        Assert.Equal(1, reader.GetInt32(4)); // bool persisted as 0/1
        Assert.Equal("Fall 2002", reader.GetString(5));
        Assert.False(reader.Read());

        // Nullable DateTime round-trips through the real class.
        var reloaded = await db.GetCourseAsync("hst-508-genomics-fall-2002");
        Assert.NotNull(reloaded);
        Assert.Equal(scraped, reloaded!.LastScrapedUtc);
    }

    [Fact]
    public async Task UpsertCourseAsync_ExistingCourse_UpdatesRowInPlace()
    {
        var db = CreateDatabase(out var dbFile);
        await db.UpsertCourseAsync(new Course { Id = "c1", Title = "Old Title" });

        await db.UpsertCourseAsync(new Course { Id = "c1", Title = "New Title", Department = "Physics" });

        Assert.Equal(1, CountRows(dbFile, "Course"));
        var reloaded = await db.GetCourseAsync("c1");
        Assert.NotNull(reloaded);
        Assert.Equal("New Title", reloaded!.Title);
        Assert.Equal("Physics", reloaded.Department);
    }

    [Fact]
    public async Task GetAllCoursesAsync_ReturnsEveryCourse()
    {
        var db = CreateDatabase();
        await db.UpsertCourseAsync(new Course { Id = "c1", Title = "One" });
        await db.UpsertCourseAsync(new Course { Id = "c2", Title = "Two" });

        var all = await db.GetAllCoursesAsync();

        Assert.Equal(2, all.Count);
    }

    [Fact]
    public async Task GetCourseAsync_UnknownId_ReturnsNull()
    {
        var db = CreateDatabase();

        Assert.Null(await db.GetCourseAsync("nope"));
    }

    // ---- Artifacts ----

    [Fact]
    public async Task UpsertArtifactAsync_NewArtifacts_AssignAutoincrementIds()
    {
        var db = CreateDatabase();
        var first = new Artifact { CourseId = "c1", SourceUrl = "https://a", Title = "A" };
        var second = new Artifact { CourseId = "c1", SourceUrl = "https://b", Title = "B" };

        await db.UpsertArtifactAsync(first);
        await db.UpsertArtifactAsync(second);

        Assert.NotEqual(0, first.Id);
        Assert.NotEqual(0, second.Id);
        Assert.NotEqual(first.Id, second.Id);
    }

    [Fact]
    public async Task FindArtifactBySourceUrlAsync_KnownUrl_ReturnsArtifact()
    {
        var db = CreateDatabase();
        await db.UpsertArtifactAsync(new Artifact
        {
            CourseId = "c1", SourceUrl = "https://ocw.mit.edu/notes.pdf", Title = "Notes",
        });

        var found = await db.FindArtifactBySourceUrlAsync("c1", "https://ocw.mit.edu/notes.pdf");
        var missing = await db.FindArtifactBySourceUrlAsync("c1", "https://ocw.mit.edu/other.pdf");
        var wrongCourse = await db.FindArtifactBySourceUrlAsync("c2", "https://ocw.mit.edu/notes.pdf");

        Assert.NotNull(found);
        Assert.Equal("Notes", found!.Title);
        Assert.Null(missing);
        Assert.Null(wrongCourse);
    }

    [Fact]
    public async Task DownloadState_PersistsAcrossDatabaseInstances()
    {
        // Resume-after-restart: a download paused at 4096 bytes must still be
        // paused at 4096 bytes when a fresh CourseDatabase opens the same file.
        var db = CreateDatabase(out var dbFile);
        var artifact = new Artifact
        {
            CourseId = "c1",
            SourceUrl = "https://ocw.mit.edu/big.zip",
            Title = "Big Zip",
            FileType = ArtifactFileType.Zip,
            DownloadStatus = DownloadStatus.InProgress,
            BytesDownloaded = 4096,
            FileSizeBytes = 1_000_000,
            Progress = 0.004096,
        };
        await db.UpsertArtifactAsync(artifact);

        // Simulate progress, then "restart" with a brand-new instance.
        artifact.BytesDownloaded = 8192;
        artifact.Progress = 0.008192;
        artifact.DownloadStatus = DownloadStatus.Paused;
        await db.UpsertArtifactAsync(artifact);

        var reopened = new CourseDatabase(new TempAppPaths(Path.GetDirectoryName(dbFile)!));
        var artifacts = await reopened.GetArtifactsForCourseAsync("c1");

        var reloaded = Assert.Single(artifacts);
        Assert.Equal(DownloadStatus.Paused, reloaded.DownloadStatus);
        Assert.Equal(8192, reloaded.BytesDownloaded);
        Assert.Equal(0.008192, reloaded.Progress, precision: 6);
        Assert.Equal(ArtifactFileType.Zip, reloaded.FileType);
    }

    [Fact]
    public async Task UpsertArtifactAsync_ExistingArtifact_UpdatesInPlaceWithoutDuplicating()
    {
        var db = CreateDatabase(out var dbFile);
        var artifact = new Artifact { CourseId = "c1", SourceUrl = "https://a", Title = "A" };
        await db.UpsertArtifactAsync(artifact);

        artifact.DownloadStatus = DownloadStatus.Completed;
        artifact.LocalFilePath = "c1/a.pdf";
        await db.UpsertArtifactAsync(artifact);

        Assert.Equal(1, CountRows(dbFile, "Artifact"));
        var reloaded = await db.FindArtifactBySourceUrlAsync("c1", "https://a");
        Assert.NotNull(reloaded);
        Assert.Equal(DownloadStatus.Completed, reloaded!.DownloadStatus);
        Assert.Equal("c1/a.pdf", reloaded.LocalFilePath);
    }

    // ---- Lectures ----

    [Fact]
    public async Task UpsertLectureAsync_NewLecture_AssignsIdAndRoundTrips()
    {
        var db = CreateDatabase();
        var lecture = new Lecture
        {
            CourseId = "c1",
            Title = "Lecture 1: Introduction",
            VideoUrl = "https://ocw.mit.edu/lec1.mp4",
            DurationSeconds = 3600,
        };

        await db.UpsertLectureAsync(lecture);

        Assert.NotEqual(0, lecture.Id);
        var lectures = await db.GetLecturesForCourseAsync("c1");
        var reloaded = Assert.Single(lectures);
        Assert.Equal("Lecture 1: Introduction", reloaded.Title);
        Assert.Equal(3600, reloaded.DurationSeconds);
    }

    [Fact]
    public async Task FindLectureAsync_ByVideoUrl_ReturnsLecture()
    {
        var db = CreateDatabase();
        await db.UpsertLectureAsync(new Lecture
        {
            CourseId = "c1", Title = "L1", VideoUrl = "https://ocw.mit.edu/lec1.mp4",
        });

        var found = await db.FindLectureAsync("c1", "https://ocw.mit.edu/lec1.mp4", "L1");

        Assert.NotNull(found);
        Assert.Equal("L1", found!.Title);
        Assert.Null(await db.FindLectureAsync("c1", "https://ocw.mit.edu/missing.mp4", "L1"));
    }

    [Fact]
    public async Task FindLectureAsync_EmptyVideoUrl_FallsBackToTitleMatch()
    {
        var db = CreateDatabase();
        // YouTube-only lecture: no direct file, VideoUrl empty.
        await db.UpsertLectureAsync(new Lecture { CourseId = "c1", Title = "L1", VideoUrl = "" });
        // Same title but a real file URL. Must NOT match the title fallback.
        await db.UpsertLectureAsync(new Lecture
        {
            CourseId = "c1", Title = "L1", VideoUrl = "https://ocw.mit.edu/lec1.mp4",
        });

        var found = await db.FindLectureAsync("c1", "", "L1");

        Assert.NotNull(found);
        Assert.Equal("", found!.VideoUrl);
    }

    [Fact]
    public async Task UpdateWatchProgressAsync_UpdatesPositionAndCompletionFlag()
    {
        var db = CreateDatabase();
        var lecture = new Lecture { CourseId = "c1", Title = "L1", DurationSeconds = 3600 };
        await db.UpsertLectureAsync(lecture);

        await db.UpdateWatchProgressAsync(lecture.Id, 1800, isCompleted: false);
        var midway = await db.FindLectureAsync("c1", "", "L1");
        Assert.NotNull(midway);
        Assert.Equal(1800, midway!.LastWatchedPositionSeconds);
        Assert.False(midway.IsCompleted);

        await db.UpdateWatchProgressAsync(lecture.Id, 3600, isCompleted: true);
        var done = (await db.GetLecturesForCourseAsync("c1")).Single();
        Assert.Equal(3600, done.LastWatchedPositionSeconds);
        Assert.True(done.IsCompleted);
    }

    [Fact]
    public async Task UpdateWatchProgressAsync_UnknownLectureId_IsNoOp()
    {
        var db = CreateDatabase();

        // Must not throw. Callers fire-and-forget progress updates.
        await db.UpdateWatchProgressAsync(999_999, 10, isCompleted: false);
    }

    // ---- Storage accounting ----

    [Fact]
    public async Task GetTotalBytesDownloadedAsync_SumsOnlyCompletedDownloads()
    {
        var db = CreateDatabase();
        await db.UpsertArtifactAsync(new Artifact
        {
            CourseId = "c1", SourceUrl = "https://a", DownloadStatus = DownloadStatus.Completed,
            FileSizeBytes = 1000,
        });
        await db.UpsertArtifactAsync(new Artifact
        {
            CourseId = "c1", SourceUrl = "https://b", DownloadStatus = DownloadStatus.InProgress,
            FileSizeBytes = 5000,
        });
        await db.UpsertLectureAsync(new Lecture
        {
            CourseId = "c1", Title = "L1", DownloadStatus = DownloadStatus.Completed,
            FileSizeBytes = 2000,
        });
        await db.UpsertLectureAsync(new Lecture
        {
            CourseId = "c1", Title = "L2", DownloadStatus = DownloadStatus.Paused,
            FileSizeBytes = 7000,
        });

        Assert.Equal(3000, await db.GetTotalBytesDownloadedAsync());
    }

    // ---- Deletion ----

    [Fact]
    public async Task DeleteCourseDataAsync_RemovesOnlyTargetCourseRows()
    {
        var db = CreateDatabase(out var dbFile);
        await db.UpsertCourseAsync(new Course { Id = "c1", Title = "One" });
        await db.UpsertCourseAsync(new Course { Id = "c2", Title = "Two" });
        await db.UpsertArtifactAsync(new Artifact { CourseId = "c1", SourceUrl = "https://a" });
        await db.UpsertArtifactAsync(new Artifact { CourseId = "c2", SourceUrl = "https://b" });
        await db.UpsertLectureAsync(new Lecture { CourseId = "c1", Title = "L1" });
        await db.UpsertLectureAsync(new Lecture { CourseId = "c2", Title = "L2" });

        await db.DeleteCourseDataAsync("c1");

        Assert.Null(await db.GetCourseAsync("c1"));
        Assert.Empty(await db.GetArtifactsForCourseAsync("c1"));
        Assert.Empty(await db.GetLecturesForCourseAsync("c1"));
        (await db.GetCourseAsync("c2")).Should().NotBeNull("the other course must survive deleting c1");
        CountRows(dbFile, "Artifact", "CourseId = 'c2'").Should().Be(1, "the other course's artifacts must survive deleting c1");
        CountRows(dbFile, "Lecture", "CourseId = 'c2'").Should().Be(1, "the other course's lectures must survive deleting c1");
    }

    [Fact]
    public async Task DeleteCourseDataAsync_UnknownCourseId_IsNoOp()
    {
        var db = CreateDatabase();

        await db.DeleteCourseDataAsync("ghost");

        Assert.Empty(await db.GetAllCoursesAsync());
    }

    // ---- Get by ID ----

    [Fact]
    public async Task GetArtifactAsync_KnownId_ReturnsArtifact()
    {
        var db = CreateDatabase();
        var artifact = new Artifact { CourseId = "c1", SourceUrl = "https://a", Title = "Notes" };
        await db.UpsertArtifactAsync(artifact);

        var found = await db.GetArtifactAsync(artifact.Id);

        Assert.NotNull(found);
        Assert.Equal("Notes", found!.Title);
        Assert.Null(await db.GetArtifactAsync(999_999));
    }

    [Fact]
    public async Task GetLectureAsync_KnownId_ReturnsLecture()
    {
        var db = CreateDatabase();
        var lecture = new Lecture { CourseId = "c1", Title = "L1", VideoUrl = "https://a/l.mp4" };
        await db.UpsertLectureAsync(lecture);

        var found = await db.GetLectureAsync(lecture.Id);

        Assert.NotNull(found);
        Assert.Equal("L1", found!.Title);
        Assert.Null(await db.GetLectureAsync(999_999));
    }

    // ---- Per-item deletion ----

    [Fact]
    public async Task DeleteArtifactAsync_RemovesOnlyThatRow()
    {
        var db = CreateDatabase(out var dbFile);
        var keep = new Artifact { CourseId = "c1", SourceUrl = "https://keep", Title = "Keep" };
        var drop = new Artifact { CourseId = "c1", SourceUrl = "https://drop", Title = "Drop" };
        await db.UpsertArtifactAsync(keep);
        await db.UpsertArtifactAsync(drop);

        await db.DeleteArtifactAsync(drop.Id);

        Assert.Null(await db.GetArtifactAsync(drop.Id));
        CountRows(dbFile, "Artifact", $"Id = {drop.Id}").Should().Be(
            0, "the row must be gone from the SQLite file, not just the ORM cache");
        (await db.GetArtifactAsync(keep.Id)).Should().NotBeNull("the sibling artifact must survive");
        await db.DeleteArtifactAsync(999_999);
    }

    [Fact]
    public async Task DeleteLectureAsync_RemovesOnlyThatRow()
    {
        var db = CreateDatabase(out var dbFile);
        var keep = new Lecture { CourseId = "c1", Title = "Keep" };
        var drop = new Lecture { CourseId = "c1", Title = "Drop" };
        await db.UpsertLectureAsync(keep);
        await db.UpsertLectureAsync(drop);

        await db.DeleteLectureAsync(drop.Id);

        Assert.Null(await db.GetLectureAsync(drop.Id));
        CountRows(dbFile, "Lecture", $"Id = {drop.Id}").Should().Be(
            0, "the row must be gone from the SQLite file, not just the ORM cache");
        (await db.GetLectureAsync(keep.Id)).Should().NotBeNull("the sibling lecture must survive");
        await db.DeleteLectureAsync(999_999);
    }
}
