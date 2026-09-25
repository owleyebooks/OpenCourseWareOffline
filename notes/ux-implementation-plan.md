# UX feedback implementation plan (2026-09-25)

Source: notes/ux-review-feedback.md (crew of 3 reviewers, synthesized 2026-09-25).
Branch: screenshot-run. Mike's directive: implement all of it, plus last-viewed course and the offline indicator,
then report. He has the final call on the brief's open questions; everything else the implementer decides.

## Non-negotiables (Mike's words, confirmed 2026-09-24)

1. Core flow stays: look up a course, download its materials, watch offline.
2. Offline-first, no accounts. Fully usable offline once downloaded.
3. MIT attribution + not-affiliated disclaimer stay.
4. Copy stays plain-language, no developer jargon. No em dashes anywhere (prose lint enforces).

## Work items

### A. Browse / Course page (CoursePage + CourseViewModel)
1. First-run state: hide the Resources/Lecture Videos toggles and the lists until the first successful fetch.
   Show a 3-step intro instead: "Paste a course link. Tap Get course. Download what you want." It must also
   state the offline model up front ("downloaded courses work with no signal").
2. Rename the "Fetch Course" button to "Get course".
3. Remove the "Total storage used" line from Browse (Downloads owns storage now).
4. Collapse the up-to-four buttons per row into ONE stateful primary button whose label/action follows state
   (Download / Pause while downloading / Watch or View once downloaded). Keep Delete reachable as a subtle
   secondary action on the row.
5. Replace the thin italic status line with an error panel in the results area when a fetch fails. It names
   the problem ("Couldn't reach ocw.mit.edu" vs "We couldn't find a course at that address"), offers one
   action (Try Again / refocus the field), and reminds: "Your downloaded courses still work offline. Open
   Downloads to watch them." (with a working link to Downloads).
6. Persistent download banner at the top of the page, bound to the shared download-status service
   ("Downloading 2 items, 43%"). Visible on every screen, not just Browse.
7. Subtle offline indicator (see shared item 13).

### B. Downloads dashboard + download services
8. Dashboard lists in-progress and paused items at the TOP (with progress bar and Cancel), downloaded courses below.
9. Add Cancel to the download service: stops the transfer and deletes the partial file so stranded partials
   cannot silently occupy storage. Every active row gets a Cancel control.
10. Per-course Open action on the dashboard; tapping a downloaded course row opens its downloaded materials
    directly (no need to go back through Browse and fetch again).
11. Storage header in human-readable units (see 14).

### C. Player, viewer, About, app-level
12. VideoPlayerPage: when a lecture with saved progress is opened, show "Resuming from 12:34" (only when
    progress is past a small threshold, e.g. 10 seconds).
13. ArtifactViewerPage PDF panel: rewrite the copy as the user's action, not an engineering apology.
    Handle the no-PDF-reader case gracefully (catch the open failure, show a message naming the file location).
14. New human-readable file size formatting (MB/GB, e.g. "1.8 GB across 3 courses") used everywhere sizes appear.
    Implement as a value converter; unit test it.
15. AboutPage: headed sections; the not-affiliated disclaimer set as a visual callout.
16. Last-viewed course: persist the last loaded course id; on startup, restore it. If offline and the course
    is not downloaded, fall back gracefully to the first-run intro state (never an error screen).
17. Offline indicator: subtle, on every screen. Implementation MUST use OS connectivity-change events only
    (MAUI `Connectivity.ConnectivityChanged`), subscribed once at app level. No polling, no probing the
    internet, no periodic checks. If the event API proves unreliable or the code gets complicated, DROP the
    indicator entirely per Mike's explicit fallback ("then.. nahh"). Record the outcome in decisions.md.

## Contracts (freeze before fanning out)

- Extend the existing download service (check what exists first; do not build a parallel one): add Cancel,
  and expose observable aggregate state (active item count, overall progress) for the banner.
- One shared connectivity service wrapping MAUI Connectivity, subscribed once at app startup.
- One place for last-course persistence (Preferences-backed is fine).
- ByteSize value converter for human-readable sizes.

## Reviewer disagreements (implementer resolves as noted)

- Storage line on Browse: REMOVE (2 of 3 reviewers).
- Error presentation: use the panel (5), not the inline line.
- Downloads Refresh button: keep only if it does something Load-on-appear does not; otherwise remove.

## Tests and hygiene

- New/updated xUnit tests: size converter, cancel deletes partial file, last-course persist/restore,
  first-run visibility properties, aggregate progress math, error panel viewmodel states.
- Full suite must pass via a VALID runner. `dotnet test` is broken in this VM (testhost exits silently;
  see ~/TOOLS.md). The /tmp reflection runner that passed 233 on 2026-09-24 is the known-good approach;
  restore and extend it. Do NOT use the 2026-09-25 replacement runner that failed 20 SQLite/constructor
  cases. Do not claim a full pass without a real green run.
- Prose lint clean (no em dashes, no diary comments).
- decisions.md entries for: offline-indicator mechanism and outcome, cancel semantics, last-course restore
  behavior, disagreement resolutions.
- Commit incrementally on screenshot-run; push when green.

## Verification

- Build the Windows target; run the full suite; prose lint.
- Make ONE attempt at a Windows screenshot run (manual workflow dispatch) to visually verify the new
  first-run state. If it fails, do not loop; note it and move on.
