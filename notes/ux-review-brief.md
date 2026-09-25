# OCW Offline — UI/UX Review Brief

Prepared 2026-09-24 for a UI/UX review crew. Feedback wanted at the design level (mockups, annotated screenshots, written notes). No implementation needed; please do not work from the XAML/C#.

## What this is

OCW Offline is a .NET MAUI app (iOS, Android, Windows) that downloads MIT OpenCourseWare course materials for offline use. Pre-store-launch, version 1.0. The author uses it himself on trains with no signal; that is the primary use case.

## Non-negotiables

These are the author's words, confirmed by Mike on 2026-09-24. Do not redesign around them.

1. **The core flow is fixed:** look up a course, download its materials, watch them offline. That is the product.
2. **Offline-first, no accounts.** The app must work fully offline once content is downloaded. It is not a cloud, social, or sync product.
3. **MIT attribution and the not-affiliated disclaimer stay.** Course content is CC BY-NC-SA (MIT OpenCourseWare); the app states it is not affiliated with or endorsed by MIT. This is a licensing requirement.

## Fair game (assistant's suggestions, not decisions)

Everything visual and structural: colors, typography, layout, tab/navigation structure, how download progress reads, empty states, error states, onboarding, and copy tone. Copy should stay plain-language; the author recently replaced developer jargon ("slug") with everyday words and wants that direction to continue.

## Screens

Screenshot of the Browse screen on Windows is in this folder: `OCW-Offline-Windows-screenshot.png`. The remaining screens are described below from the current build; more captures can be produced on request.

### 1. Browse (CoursePage) — the landing screen. See screenshot.

- Top toolbar: Browse | Downloads | About.
- A text field ("Paste a course link from ocw.mit.edu"), a Fetch Course button, and an italic hint line ("Paste the course's web address from ocw.mit.edu and tap Fetch.").
- After fetching: two toggle buttons, Resources and Lecture Videos, then a scrollable list. Each row shows the item title plus up to four small buttons: Download (label changes with state), Pause (while downloading), View or Watch (once downloaded), Delete (once downloaded). A progress bar appears under rows that are downloading.
- Bottom: "Total storage used: N bytes" in small grey text.

### 2. Browse Courses (CatalogPage) — reached via the Browse toolbar item.

- A filter field ("Filter loaded courses by title"), a status line, and a scrollable list of course titles with subtitles. Tapping a course loads it into the CoursePage. A Load More button sits at the bottom.

### 3. Downloads (DownloadsDashboardPage)

- Header: "Total: N bytes across all downloaded courses" in bold.
- A list of downloaded courses, each row showing the course title, bytes used, and a Delete button. Empty state reads "Nothing downloaded yet." A Refresh button sits at the bottom.

### 4. Lecture (VideoPlayerPage)

- A video player with standard playback controls and a small grey status line ("Loading...") beneath it. Watch progress is saved so lectures resume.

### 5. Viewer (ArtifactViewerPage)

- Downloaded HTML files render in an embedded viewer. PDFs cannot render offline in the embedded viewer, so the app shows an explanatory panel ("This PDF opens in your device's own PDF app...") with an Open PDF button that hands the file to the device's PDF app.

### 6. About (AboutPage)

- App name, version, the MIT OpenCourseWare CC BY-NC-SA attribution paragraph, the not-affiliated-with-MIT disclaimer, and two buttons: Privacy Policy and Contribute on GitHub.

## Flows to review

1. **First run.** The user lands on Browse with an empty field. Is it obvious what to do? Should the field keep a pre-filled example course?
2. **Fetch to results.** After tapping Fetch Course, the Resources/Lecture Videos lists appear. Is the transition clear? Is the loading state adequate?
3. **Download progress.** Each row has its own progress bar; the dashboard shows totals. Does progress read at a glance? Is Pause/Delete placement sensible?
4. **Empty and error states.** Nothing downloaded yet; fetch failures; no network. Current copy is minimal ("Nothing downloaded yet.").
5. **Video watching.** Entering the player, resuming a lecture, and knowing progress was saved.

## Open questions (for the author, not the crew to decide alone)

- Tab bar vs. the current toolbar-button navigation?
- Keep the pre-filled example course in the Browse field or start empty?
- Keep the stock native look or adopt a custom visual theme?

## Out of scope

- Implementation details (XAML, C#, project structure).
- The three non-negotiables above.
- Anything requiring a server, an account system, or cloud sync.
