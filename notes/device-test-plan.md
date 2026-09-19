# OCW Offline Device Test Plan: FIRST_BUILD_CHECKLIST's Four Platform Risks

**Source:** `archive/v43-process-rules/HANDOFF_v43.md` backlog + `drop/docs/FIRST_BUILD_CHECKLIST.md` (the v43 bridge doc, verified against the v43 tree).
**Code under test:** `work/OcwOffline/` (v43 code, never compiled per PROCESS Rule 1).
**Status as of 2026-09-18:** /dev/kvm does not exist in this sandbox → no Android emulator here; no Mac and no iOS hardware → items 1 (iOS) cannot run at all yet. Everything below is a concrete plan for a real device/emulator session later. Nothing in this plan requires writing code: pass/fail only, plus note any workaround attempted.

**Prerequisites for all items:** a successful first compile (see `notes/assessment-2026-09-18.md`) and the app installed on the target. Unless a step says otherwise, use a debug build with the v43 code unchanged, so results describe the code as-written.

---

## Item 1: iOS WKWebView local-file access (HTML/PDF embedding)

**Applies to:** iOS only. **Needs physical iOS device (cannot run on an Android emulator). No Mac exists today, so this whole item is deferred.**

**Code:** `Views/ArtifactViewerPage.xaml.cs`: `LoadArtifact` sets
`Viewer.Source = new UrlWebViewSource { Url = new Uri(fullPath).AbsoluteUri }`
(a `file://` URI). The absolute-URI form was a deliberate v19 choice to dodge
dotnet/maui#10674's relative-URI crash. What it does *not* solve is read
access to the HTML file's sibling resources: the native iOS API is
`WKWebView.loadFileURL(_:allowingReadAccessTo:)`, and MAUI's handler may not
grant the folder scope, in which case the page loads but renders blank or as
raw/unstyled text.

**Steps:**
1. On the iOS device, in the app: download a course that contains an HTML
   artifact which references **local** CSS/images/JS (not just a single
   self-contained `.html`; a bare single file will not exercise this).
2. Open the artifact via the Course page "View" button → `ArtifactViewerPage`
   with `Viewer.IsVisible = true`.
3. Observe: does the page render fully styled with images, or raw/unstyled/blank?

**Expected / pass:** fully styled page with all local resources loaded.
**Fail signatures:** page renders as unstyled text, blank white, or missing
images/CSS while the main text shows.

**If it fails:** the documented fix is a custom `WebViewHandler` under
`Platforms/iOS/` that calls `loadFileURL(_:allowingReadAccessTo:)` with read
access to the artifact's folder, instead of relying on `UrlWebViewSource`.
Re-test the same artifact after the change.

---

## Item 2: MediaElement seek-on-open race (video resume)

**Applies to:** iOS + Android. **Needs a physical device; an Android emulator
may be attempted later but a pass there is not conclusive**, the race is
against real decoder timing (ExoPlayer/Media3 on Android, AVPlayer on iOS),
which emulators don't reproduce faithfully.

**Code:** `Views/VideoPlayerPage.xaml.cs`: `OnMediaOpened` (fires on
`MediaOpened`) immediately does
`await Player.SeekTo(TimeSpan.FromSeconds(_lecture.LastWatchedPositionSeconds), CancellationToken.None)`.
Note `ShouldAutoPlay="False"` in `VideoPlayerPage.xaml`; the player is not
playing when the seek fires. This matches the documented
seek-after-MediaOpened pattern, but whether each platform's decoder honors a
seek issued that early is unverified.

**Steps:**
1. Download a course video. Play it, let it run to ~2–3 minutes, then navigate
   back (this persists `LastWatchedPositionSeconds` via `OnDisappearing` →
   `SaveProgressAsync`; confirm the lecture row now shows the position).
2. Re-open the same lecture.
3. Observe: does playback resume at the saved position, start at 0:00, or
   stutter/glitch on open?

**Expected / pass:** resumes cleanly at the saved position, no stutter.
**Fail signatures:** starts at 0:00; visible stutter/glitch on open; seek
thrown away (resume position lost).

**If it fails:** the documented workaround is to defer the `SeekTo` until the
player's state reaches `Playing`/`Paused` (CommunityToolkit MediaElement
exposes `CurrentState`/`StateChanged`) rather than firing on `MediaOpened`.
Re-test the same scenario after the change, on **both** platforms (ExoPlayer
and AVPlayer behave differently here).

---

## Item 3: Android PDF hand-off via FileProvider

**Applies to:** Android only. **Could run on an Android emulator later**
(FileProvider content URIs and the app-chooser work there), but install a
PDF reader app on the emulator first, and a physical Android 14+ device is the
stronger confirmation.

**Code:** `Views/ArtifactViewerPage.xaml.cs`: `OnOpenPdfClicked` copies the
PDF to `FileSystem.CacheDirectory/sharing-root/` and calls
`Launcher.Default.OpenAsync(new OpenFileRequest(...))`. This relies on
Essentials' **bundled** FileProvider (`microsoft_maui_essentials_fileprovider_file_paths.xml`
maps `cache-path` → `sharing-root` (verified live at v19, which is why no
custom `<provider>` was declared: colliding authorities are a known bug
source). `Platforms/Android/AndroidManifest.xml` carries the `<queries>` block
(`ACTION_VIEW` + `application/pdf`) added at v21 for Android 11+ package
visibility.

**Steps:**
1. On a real Android device (preferably API 34 or 35), download a course with
   a PDF artifact.
2. On Android, `CanEmbed` returns false for PDF → the page shows `PdfPanel`
   with "Tap Open PDF…". Tap it.
3. Observe: does the OS app-chooser appear and does the chosen PDF app open
   the document readable?

**Expected / pass:** chooser appears, PDF app opens and renders the file.
**Fail signatures:** `SecurityException` / `IllegalArgumentException: Failed to
find configured root` (FileProvider path mapping broken); the PDF app opens
but reports "can't read file" (URI permission grant missing); no chooser and
nothing happens (package visibility still filtered despite `<queries>`).

**If it fails:** likely fix is `Platforms/Android/Resources/xml/provider_paths.xml`
explicitly granting the path, per the checklist; but do **not** also declare a
custom `<provider>` authority in the manifest without care (authority collision
with Essentials' own is a documented bug source). Re-test on the same API
level after the change.

---

## Item 4: CollectionView thrash from download-progress updates

**Applies to:** Android + iOS. **Could run on an Android emulator later**
(scroll smoothness/jank is render-loop behavior and is observable there); a
physical device (especially a lower-end one) is the stronger test.

**Code:** `Services/DownloadManager.cs` throttles progress events to
`minReportIntervalMs = 100`; `ViewModels/CourseViewModel.cs` →
`OnProgressChanged` hops to the UI thread via
`IMainThreadDispatcher.BeginInvokeOnMainThread` and sets two
`[ObservableProperty]` fields (`BytesDownloaded`, `Progress`) on the bound
`Artifact`/`Lecture`; each set raises `PropertyChanged`, and
`Views/CoursePage.xaml` binds both `CollectionView` row templates'
`ProgressBar Progress="{Binding Progress}"` to it.

**Steps:**
1. On the device, open a course with several large artifacts/lectures.
2. Start downloads of **multiple large files at once** (the throttle is
   per-download: N concurrent downloads = N events per 100 ms, all hitting
   the UI thread).
3. While downloading, **scroll the course's CollectionView up and down** and
   watch the per-row progress bars.

**Expected / pass:** scrolling stays smooth; progress bars animate cleanly to
100% and rows flip to Completed without flicker.
**Fail signatures:** visible scroll stutter/jank while progress updates flow;
per-row flicker or layout jumps.

**If it fails:** the documented one-line tuning is to raise
`minReportIntervalMs` in `DownloadManager.cs` from 100 to 250 or 500 and
re-test the same scenario. If 250 still janks on that device, try 500.
(Trade-off is only how live the progress bar feels; completion correctness
is unaffected: the final tally is always reported unconditionally after the
read loop.)

---

## General notes for the device session

- Test each item **independently** on its listed platform(s); don't batch
  fixes across items before re-testing.
- Record for each item: device model, OS/API level, build configuration,
  and the exact pass/fail signature observed (quote error text verbatim).
- If any item passes on the first try, still record the device/API: "passed
  on Pixel X / API 35" is the evidence that closes the checklist row.
