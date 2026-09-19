# Stream 2 report: SQLite integration tests (2026-09-18)

## Implementation under test

`work/OcwOffline/Services/CourseDatabase.cs`: the real `ICourseDatabase`
implementation. It is a thin async wrapper around **sqlite-net-pcl**
(`SQLiteAsyncConnection`), not EF Core: one lazily-created connection reused
for the app's lifetime, schema from the `[PrimaryKey]`/`[Indexed]` attributes
on the models in `Models/`, created via `CreateTableAsync<T>` on first use
under a `SemaphoreSlim` double-checked guard.

**One production change was required to test it** (needs a `decisions.md`
entry; I did not touch that file): `CourseDatabase` hardcoded its path via
the static `AppPaths.Root`, and `FileSystem.AppDataDirectory` throws
`NotImplementedInReferenceAssemblyException` on the bare net10.0 test host
(same reason the v43 fixup created the `IAppPaths` seam). I added an optional
constructor parameter `IAppPaths? paths = null`; when omitted (production
`MauiProgram` registration: `AddSingleton<CourseDatabase>()`) behavior is byte
for byte identical; the default parameter keeps DI activation working.
Tests inject a per-test temp directory. No app `.csproj` was touched;
`drop/` untouched; `STATUS.md` untouched.

Deliberate standards deviation to record: cross-project `testing.md` wants
integration tests against real PostgreSQL via Testcontainers. This project's
data layer is SQLite by product design (offline-first MAUI app, single-user
on-device DB); these tests run against real temp-file SQLite instead. Same
spirit (real database, no mocks), different engine.

## Tests added

New file: `work/OcwOffline.Tests/Services/CourseDatabaseIntegrationTests.cs`
(19 tests, xunit, file-scoped namespace, nullable). Each test gets a unique
temp dir (parallel-safe); the DB is **driven** through the real
`CourseDatabase` via `ICourseDatabase` and **verified** through an independent
`Microsoft.Data.Sqlite` channel (raw `sqlite_master` reads, row counts, column
values); cross-library proof that schema and rows actually land in the file.
`Microsoft.Data.Sqlite` 10.0.12 was added to the **test** csproj only, and
`Services/CourseDatabase.cs` is file-linked into the test project.

| Test | Covers |
|---|---|
| `FirstUse_CreatesDatabaseFileWithExpectedTables` | Schema creation: db file created, `Course`/`Artifact`/`Lecture` tables exist (via `sqlite_master`) |
| `FirstUse_CreatesCourseIdIndexes` | `[Indexed] CourseId` on Artifact/Lecture materializes as real SQLite indexes |
| `ConcurrentFirstAccess_AllCallersGetUsableConnection` | 8 concurrent first-use calls race the schema-creation guard: no "no such table" |
| `UpsertCourseAsync_NewCourse_PersistsAllColumnsToSqliteFile` | Course CRUD round-trip; raw column values incl. bool-as-int; nullable `DateTime?` round-trip |
| `UpsertCourseAsync_ExistingCourse_UpdatesRowInPlace` | Upsert = insert-or-update, single row, no duplicate |
| `GetAllCoursesAsync_ReturnsEveryCourse` | Multi-course read |
| `GetCourseAsync_UnknownId_ReturnsNull` | Null for missing course |
| `UpsertArtifactAsync_NewArtifacts_AssignAutoincrementIds` | Autoincrement Id assignment (1, 2, distinct) |
| `FindArtifactBySourceUrlAsync_KnownUrl_ReturnsArtifact` | Reconciliation lookup: hit, URL miss, and wrong-course miss all correct |
| `DownloadState_PersistsAcrossDatabaseInstances` | **Download resume**: paused-at-8192-bytes artifact written by instance A is read back identically by a fresh instance B on the same file (restart survival) |
| `UpsertArtifactAsync_ExistingArtifact_UpdatesInPlaceWithoutDuplicating` | Re-upsert updates status/path in place, single row |
| `UpsertLectureAsync_NewLecture_AssignsIdAndRoundTrips` | Lecture insert + read incl. `DurationSeconds` |
| `FindLectureAsync_ByVideoUrl_ReturnsLecture` | VideoUrl-keyed lookup + miss |
| `FindLectureAsync_EmptyVideoUrl_FallsBackToTitleMatch` | Empty-VideoUrl falls back to title; a same-title lecture *with* a VideoUrl does NOT match the fallback (pins documented behavior) |
| `UpdateWatchProgressAsync_UpdatesPositionAndCompletionFlag` | Watch-progress write path |
| `UpdateWatchProgressAsync_UnknownLectureId_IsNoOp` | No throw on unknown id (fire-and-forget callers) |
| `GetTotalBytesDownloadedAsync_SumsOnlyCompletedDownloads` | Storage accounting: Completed artifact+lecture counted, InProgress/Paused excluded (3000 = 1000 + 2000) |
| `DeleteCourseDataAsync_RemovesOnlyTargetCourseRows` | Cascade delete of one course's rows; other course's rows intact (verified raw-SQL too) |
| `DeleteCourseDataAsync_UnknownCourseId_IsNoOp` | No throw on unknown course |

Transaction behavior note: the implementation promises no explicit
transactions; each method is independent awaits on the shared connection, and
the only concurrency control is the first-use schema guard, which is what
`ConcurrentFirstAccess` pins. There was nothing else transactional to test.

## Full-suite results

- Build: `dotnet build` on `OcwOffline.Tests.csproj`: **0 warnings, 0 errors**.
  (`dotnet test`/vstest is broken here; `dotnet restore`/`build` on the full
  `.slnx` also fails on missing MAUI workloads for the app's mobile TFMs,
  pre-existing, unrelated. The test project is plain net10.0 and builds alone.)
- Runner: `dotnet exec /tmp/xunit-console/tools/net6.0/xunit.console.dll
  OcwOffline.Tests.dll` needed `DOTNET_ROLL_FORWARD=LatestMajor` (console
  targets net6.0, only the 10.0.12 runtime is installed); worth adding to
  `notes/env-setup.md`.
- **227/227 green, 0 failed, 0 skipped, 0 errors**; all 208 pre-existing tests
  plus the 19 new integration tests. New class run in isolation: 19/19 pass.

## NU1903 finding: SQLitePCLRaw.lib.e_sqlite3 2.1.2

- **Current version: 2.1.2** (via `sqlite-net-pcl 1.9.172` →
  `SQLitePCLRaw.bundle_green 2.1.2` → `lib.e_sqlite3 2.1.2`). Confirmed live
  on nuget.org with vulnerability **GHSA-2m69-gcr7-jv3q, severity high**.
- **Fixed version: 2.1.12** (published 2026-07-14; first 2.1.x with no
  advisory entry). Latest in the line is **2.1.13** (2026-08-13); there is
  also a 3.53.3 under the new versioning scheme.
- **I did not change any version** (per instructions). Side effect worth
  knowing: adding `Microsoft.Data.Sqlite` 10.0.12 to the *test* project pulled
  `SQLitePCLRaw.bundle_e_sqlite3 2.1.12`, which raised the resolved
  `lib.e_sqlite3` to **2.1.12** in the test project's graph; NU1903 no longer
  appears on test-project restore. The **app** project (`OcwOffline.csproj`)
  still resolves 2.1.2 and still carries the advisory.
- **Bump safety assessment**: bumping the app to `SQLitePCLRaw.bundle_green`
  2.1.12/2.1.13 looks low-risk; SQLitePCLRaw 2.1.x is API-stable across these
  patch releases and sqlite-net-pcl 1.9.172 only needs `bundle_green >= 2.1.2`,
  so a direct `<PackageReference>` pin on `bundle_green` 2.1.12 in the app
  csproj would lift `lib.e_sqlite3` without touching sqlite-net-pcl. Still,
  the app's mobile TFMs (iOS/Android native `e_sqlite3` binaries) can't be
  exercised on this Linux host; the coordinator should verify on device/CI
  before shipping the bump.

## Open items for the coordinator

1. Add a `notes/decisions.md` entry for the `CourseDatabase(IAppPaths? = null)`
   test seam (production behavior unchanged).
2. Decide on the `bundle_green` → 2.1.12/2.1.13 bump for the app project to
   clear NU1903 there (I changed nothing).
3. Consider adding `DOTNET_ROLL_FORWARD=LatestMajor` to `notes/env-setup.md`
   for the xunit console runner invocation.
4. Record the testing.md deviation (SQLite instead of PostgreSQL/Testcontainers
   for this project's integration tests) if not already covered.
