# UX implementation: frozen contracts (2026-09-25)

Committed on `screenshot-run` before the A/B/C worker fan-out. Workers build
to these signatures; coordinator merges.

## IDownloadManager (extended, Services/IDownloadManager.cs)

Existing members unchanged. New:

- `event Action<DownloadAggregate>? AggregateChanged` — fires on download
  start, each throttled progress tick, completion, pause, and cancel.
- `Task DownloadArtifactAsync(Artifact artifact)` — item-level orchestration
  moved out of CourseViewModel: drives InProgress to Completed (extracting
  zips via ExtractZipAsync), Paused, or Failed; updates the entity's
  Progress/BytesDownloaded live via IMainThreadDispatcher. Throws
  DownloadStalledException on watchdog stall so the caller can message it.
- `Task DownloadLectureAsync(Lecture lecture)` — same for lectures.
- `void Cancel(string progressKey)` — cancels the CTS, deletes the partial
  file, resets the entity to NotStarted with zeroed progress. No-op on an
  unknown key. Pause keeps partial bytes; Cancel deletes them.
- `IReadOnlyList<ActiveDownload> GetActiveDownloads()` — latest per-key
  snapshots for the dashboard's active section.
- `DownloadAggregate GetAggregate()` — current roll-up.

`DownloadManager` ctor becomes
`DownloadManager(ICourseDatabase db, IMainThreadDispatcher mainThread,
IAppPaths appPaths, HttpMessageHandler? httpHandler = null)`. The optional
handler is the network seam for tests (production DI uses the default).
`MauiProgram` registration is unchanged (`AddSingleton<DownloadManager>()`
still resolves; the optional parameter defaults).

## DownloadAggregate (Services/DownloadAggregate.cs)

`record DownloadAggregate(int ActiveCount, double OverallFraction,
long BytesReceived, long TotalBytes)` with `static DownloadAggregate
Compute(IEnumerable<DownloadProgress>)`: weighted by TotalBytes; snapshots
with TotalBytes 0 count toward ActiveCount but not the fraction; empty in
gives `None` (0,0,0,0).

## ActiveDownload (Services/ActiveDownload.cs)

`record ActiveDownload(string Key, long BytesReceived, long TotalBytes)`
with `Fraction`. Keys keep the "artifact-{id}" / "lecture-{id}" convention.

## IConnectivityService (Services/IConnectivityService.cs)

`bool IsConnected { get; }`, `event Action<bool>? ConnectivityChanged`.
Production `MauiConnectivityService` (worker C) subscribes to MAUI
`Connectivity.ConnectivityChanged` in its ctor, singleton. Tests use
`Tests/Fakes/FakeConnectivityService` (already written) with
`SetConnected(bool)`.

## ILastCourseStore (Services/ILastCourseStore.cs)

`string? GetLastCourseId()`, `void SetLastCourseId(string)`, `void Clear()`.
Production `PreferencesLastCourseStore` (MAUI Preferences, key
"ocw.lastCourseId"). Tests use `InMemoryLastCourseStore` (already written).

## ByteSizeConverter (Views/ByteSizeConverter.cs)

`IValueConverter` with `static string Format(long bytes)`: under 1024 gives
"512 bytes"; KB/MB/GB with one decimal, whole units show no decimals
("5 MB", "1.8 GB").

## ICourseDatabase additions (worker B implements)

- `Task<Artifact?> GetArtifactAsync(int artifactId)`
- `Task<Lecture?> GetLectureAsync(int lectureId)`

Needed to join the manager's active-download keys back to titled entities
on the dashboard. Implement in CourseDatabase and FakeCourseDatabase.

## AppStatusViewModel (worker C, ViewModels/, singleton)

- `bool HasActiveDownloads`, `string ActiveDownloadText`
  ("Downloading 2 items, 43%"; singular "1 item"), `bool IsOffline`.
- Subscribes to `IDownloadManager.AggregateChanged` and
  `IConnectivityService.ConnectivityChanged`; dispatches to main thread.
- `[RelayCommand] ShowActiveDownloads` raises
  `event Action? NavigateToDownloadsRequested` (pages with a dashboard
  factory subscribe).

## StatusBannerView (worker C, Views/StatusBannerView.xaml)

ContentView bound to AppStatusViewModel (each page sets the BindingContext
from its injected singleton): download banner visible when
HasActiveDownloads (tap runs ShowActiveDownloadsCommand), subtle offline
line visible when IsOffline ("You're offline. Downloaded courses still
work."). Included at the top of every page's layout by whichever worker
owns that page.

## CourseViewModel additions (worker A)

- `bool HasCompletedFetch` (toggles/lists visible only after first success),
  `string FetchedCourseTitle` (success header).
- Error panel: `bool HasFetchError`, `string FetchErrorTitle`,
  `string FetchErrorDetail`, `bool IsNetworkError`; static
  `FetchErrorInfo ClassifyFetchError(Exception ex)` (worker A defines the
  record; network = HttpRequestException-ish, address = scraper not-found
  signal, else generic). `RetryFetchCommand`: network error re-runs fetch,
  otherwise raises `event Action? RefocusCourseEntryRequested`.
- `event Action? NavigateToDownloadsRequested` (error panel's Downloads link).
- Single stateful row actions: `PrimaryArtifactActionCommand`,
  `PrimaryLectureActionCommand` (NotStarted/Failed = download, InProgress =
  pause, Paused = resume, Completed = raise `OpenArtifactRequested` /
  `OpenLectureRequested`), `CancelArtifactCommand`, `CancelLectureCommand`.
- `Task RestoreLastCourseAsync()` (once-guarded): reads ILastCourseStore;
  if an id exists and IConnectivityService.IsConnected, sets CourseSlug
  and fetches; otherwise leaves the first-run intro. Successful fetch
  persists the id. Offline-with-id never shows an error screen.
- Ctor gains `ILastCourseStore` and `IConnectivityService`; existing test
  constructions must be updated. `_progressTargets`/`OnProgressChanged`
  go away (the manager updates entities directly now); keep the public
  command surface.

## Decisions already taken (recorded here so workers do not relitigate)

- Pre-filled course slug removed; CourseSlug defaults to empty. Placeholder
  carries the instruction.
- "Fetch Course" becomes "Get course" in UI; in-progress text "Looking up
  the course…"; field and button disable during fetch (IsBusy).
- "Resources" tab becomes "Course materials".
- Toolbar "Browse" (catalog) becomes "Course catalog"; CoursePage title
  becomes "Find a course".
- Storage line removed from Browse; dashboard header owns it as a VM
  `StorageSummary` string ("Using 1.8 GB across 3 courses").
- Downloads Refresh button removed; dashboard loads on appearing and
  follows AggregateChanged live.
- Resume threshold for the "Resuming from" banner: 30 seconds.
- Dashboard rows expand inline to per-item Open/Watch + Delete; row tap
  toggles expansion; course-level Open opens the first... (worker B: row
  tap expands; per-item Open/Watch buttons navigate via page factories
  injected into DownloadsDashboardPage).
- Error panel, not inline line, for fetch failures.
- Delete stays a subtle red-tinted text action, never the primary button.
- Offline indicator ships via ConnectivityChanged only; no polling.
