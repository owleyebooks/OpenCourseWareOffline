# OCW Offline — First Real-Build Checklist

**Source:** an external review by Gemini (pasted into chat by the person, v43),
flagging four platform-runtime behaviors that static analysis and unit tests
can't verify — per `PROCESS_v43.md` Rule 1, nothing in this project has ever
been compiled or run. Each item below was independently checked against the
actual source in this tree before being included here; none are taken on
Gemini's word alone. Added to the tree as a deliberate Rule 8 exception — see
`AUDIT_TRAIL_v43.md`'s v43 entry for why a designated scan round is landing a
new file at all.

This file has no bearing on anything until an actual compiled build exists.
It's a checklist for *that* session, not a task list for this one.

---

## 1. iOS `WKWebView` and local file access (`ArtifactViewerPage.xaml.cs`)

HTML artifacts are embedded via `UrlWebViewSource { Url = new
Uri(fullPath).AbsoluteUri }` — a `file://` URI. The code already carries a
comment citing `dotnet/maui #10674`, so this risk was flagged going in, not
newly discovered by the review.

**Watch for:** open a downloaded HTML course page that references local
CSS/JS/images (not just a single self-contained file). If it renders as
raw/unstyled text or blank, `UrlWebViewSource` isn't granting the native
`WKWebView` read access to the parent folder — iOS's native API for this is
`loadFileURL(_:allowingReadAccessTo:)`, which MAUI's wrapper doesn't always
expose cleanly. The fix, if needed, is a custom `WebViewHandler` on iOS that
calls that API directly instead of relying on `UrlWebViewSource`.

## 2. `MediaElement` seek-on-open race (`VideoPlayerPage.xaml.cs`)

`OnMediaOpened` calls `await Player.SeekTo(...)` as soon as the `MediaOpened`
event fires, to resume a partially-watched lecture. The code's own comment
already flags this as unverified pending a real build (see `AUDIT_TRAIL v17`).

**Watch for:** open a video with a saved watch position on a physical device.
If it resumes cleanly, fine. If it stutters, glitches, or resets to 0:00,
the seek is racing the platform decoder's buffer (ExoPlayer/Media3 on
Android, AVPlayer on iOS) — try waiting for the player's state to reach
`Playing`/`Paused` before calling `SeekTo`, rather than firing it the instant
`MediaOpened` fires.

## 3. Android PDF hand-off via `FileProvider` (`ArtifactViewerPage.xaml.cs`)

PDFs on Android aren't embedded — the file is copied to
`FileSystem.CacheDirectory/sharing-root` and handed off via
`Launcher.Default.OpenAsync(new OpenFileRequest(...))`, relying on MAUI
Essentials' default `FileProvider`. `AndroidManifest.xml` already declares
the `<queries>` block for `action.VIEW` + `application/pdf`, so package
visibility on Android 11+ is covered.

**Watch for:** tap "Open PDF" on a real Android 14+ (API 34/35) device. If
the OS throws a `SecurityException` or the receiving app reports the file
can't be read, Essentials' auto-generated `FileProvider` doesn't have the
`sharing-root` subfolder mapped in its paths config — the fix is a
`provider_paths.xml` under `Platforms/Android/Resources/xml/` explicitly
granting that path.

## 4. `CollectionView` thrash from progress updates (`DownloadManager.cs` / `CourseViewModel.cs`)

Download progress is throttled to `minReportIntervalMs = 100` in
`DownloadManager.cs`; `CourseViewModel.OnProgressChanged` dispatches each
event via `MainThread.BeginInvokeOnMainThread(...)` to update the bound
`ObservableCollection` row.

**Watch for:** download something large while scrolling the course's
`CollectionView`. Smooth scrolling + clean progress animation = fine. Visible
stutter or per-row flicker means 100ms is too aggressive for that cell
template on that device — bump `minReportIntervalMs` in `DownloadManager.cs`
to 250 or 500 and re-test; this is a one-line, low-risk tuning change once a
compiler is actually in the loop.

---

## Not on this list

Anything not tied to a specific file/behavior above wasn't included — this
is a checklist for a first build session, not a restatement of the whole
"nothing has ever compiled" ceiling already covered by `PROCESS_v43.md`
Rule 1 and `HANDOFF_v43.md` §3.
