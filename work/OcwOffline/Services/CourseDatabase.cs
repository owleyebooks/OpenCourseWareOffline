using OcwOffline.Models;
using SQLite;

namespace OcwOffline.Services;

/// <summary>
/// Thin async wrapper around sqlite-net-pcl. One connection, lazily opened
/// and reused for the lifetime of the app (registered as a singleton in
/// MauiProgram). All schema lives in Models/.
/// </summary>
public class CourseDatabase : ICourseDatabase
{
    private readonly string _dbPath;

    /// <summary>
    /// <paramref name="paths"/> is a test seam: on a bare net10.0 host the
    /// static <see cref="AppPaths.Root"/> throws
    /// NotImplementedInReferenceAssemblyException, so integration tests
    /// inject a temp directory. Production (MauiProgram) keeps the default,
    /// preserving on-device behavior exactly.
    /// </summary>
    public CourseDatabase(IAppPaths? paths = null)
    {
        _dbPath = Path.Combine(paths?.Root ?? AppPaths.Root, "ocw_offline.db3");
    }

    private SQLiteAsyncConnection? _connection;

    // Guards first-time connection creation: without it, two callers could
    // both see _connection as non-null before CreateTableAsync finished,
    // handing out a connection with no tables yet. See AUDIT_TRAIL v9.
    private readonly SemaphoreSlim _connectionLock = new(1, 1);

    private async Task<SQLiteAsyncConnection> GetConnectionAsync()
    {
        if (_connection is not null)
            return _connection;

        await _connectionLock.WaitAsync();
        try
        {
            if (_connection is not null)
                return _connection;

            var dbPath = _dbPath;
            var connection = new SQLiteAsyncConnection(dbPath);

            await connection.CreateTableAsync<Course>();
            await connection.CreateTableAsync<Artifact>();
            await connection.CreateTableAsync<Lecture>();

            _connection = connection;
            return _connection;
        }
        finally
        {
            _connectionLock.Release();
        }
    }

    // ---- Courses ----

    public async Task<List<Course>> GetAllCoursesAsync()
    {
        var db = await GetConnectionAsync();
        return await db.Table<Course>().ToListAsync();
    }

    public async Task<Course?> GetCourseAsync(string courseId)
    {
        var db = await GetConnectionAsync();
        return await db.Table<Course>().Where(c => c.Id == courseId).FirstOrDefaultAsync();
    }

    public async Task UpsertCourseAsync(Course course)
    {
        var db = await GetConnectionAsync();
        var existing = await GetCourseAsync(course.Id);
        if (existing is null)
            await db.InsertAsync(course);
        else
            await db.UpdateAsync(course);
    }

    // ---- Artifacts ----

    public async Task<List<Artifact>> GetArtifactsForCourseAsync(string courseId)
    {
        var db = await GetConnectionAsync();
        return await db.Table<Artifact>().Where(a => a.CourseId == courseId).ToListAsync();
    }

    public async Task UpsertArtifactAsync(Artifact artifact)
    {
        var db = await GetConnectionAsync();
        if (artifact.Id == 0)
            await db.InsertAsync(artifact);
        else
            await db.UpdateAsync(artifact);
    }

    // ---- Reconciliation for re-fetch (handoff v3 item 7) ----

    /// <summary>
    /// Looks up an existing Artifact by SourceUrl so re-Fetch updates the
    /// row in place instead of duplicating it. See AUDIT_TRAIL v4.
    /// </summary>
    public async Task<Artifact?> FindArtifactBySourceUrlAsync(string courseId, string sourceUrl)
    {
        var db = await GetConnectionAsync();
        return await db.Table<Artifact>()
            .Where(a => a.CourseId == courseId && a.SourceUrl == sourceUrl)
            .FirstOrDefaultAsync();
    }

    /// <summary>
    /// Same idea for Lectures, keyed on VideoUrl, falling back to Title
    /// when VideoUrl is empty (YouTube-only lecture, no direct file).
    /// </summary>
    public async Task<Lecture?> FindLectureAsync(string courseId, string videoUrl, string title)
    {
        var db = await GetConnectionAsync();
        if (!string.IsNullOrWhiteSpace(videoUrl))
            return await db.Table<Lecture>()
                .Where(l => l.CourseId == courseId && l.VideoUrl == videoUrl)
                .FirstOrDefaultAsync();

        return await db.Table<Lecture>()
            .Where(l => l.CourseId == courseId && l.Title == title && l.VideoUrl == "")
            .FirstOrDefaultAsync();
    }

    // ---- Lectures ----

    public async Task<List<Lecture>> GetLecturesForCourseAsync(string courseId)
    {
        var db = await GetConnectionAsync();
        return await db.Table<Lecture>().Where(l => l.CourseId == courseId).ToListAsync();
    }

    public async Task UpsertLectureAsync(Lecture lecture)
    {
        var db = await GetConnectionAsync();
        if (lecture.Id == 0)
            await db.InsertAsync(lecture);
        else
            await db.UpdateAsync(lecture);
    }

    public async Task UpdateWatchProgressAsync(int lectureId, int positionSeconds, bool isCompleted)
    {
        var db = await GetConnectionAsync();
        var lecture = await db.Table<Lecture>().Where(l => l.Id == lectureId).FirstOrDefaultAsync();
        if (lecture is null) return;

        lecture.LastWatchedPositionSeconds = positionSeconds;
        lecture.IsCompleted = isCompleted;
        await db.UpdateAsync(lecture);
    }

    // ---- Storage accounting (feeds the Downloads Manager dashboard, step 5) ----

    public async Task<long> GetTotalBytesDownloadedAsync()
    {
        var db = await GetConnectionAsync();
        var artifactBytes = await db.Table<Artifact>()
            .Where(a => a.DownloadStatus == DownloadStatus.Completed)
            .ToListAsync();
        var lectureBytes = await db.Table<Lecture>()
            .Where(l => l.DownloadStatus == DownloadStatus.Completed)
            .ToListAsync();

        return artifactBytes.Sum(a => a.FileSizeBytes) + lectureBytes.Sum(l => l.FileSizeBytes);
    }

    /// <summary>
    /// Deletes a course's DB rows entirely. Paired with DownloadManager.
    /// DeleteCourseFolder: without both, stale Completed rows would
    /// wrongly reconcile as already-downloaded on a later re-fetch.
    /// </summary>
    public async Task DeleteCourseDataAsync(string courseId)
    {
        var db = await GetConnectionAsync();

        foreach (var artifact in await db.Table<Artifact>().Where(a => a.CourseId == courseId).ToListAsync())
            await db.DeleteAsync(artifact);

        foreach (var lecture in await db.Table<Lecture>().Where(l => l.CourseId == courseId).ToListAsync())
            await db.DeleteAsync(lecture);

        var course = await GetCourseAsync(courseId);
        if (course is not null)
            await db.DeleteAsync(course);
    }
}
