# OCW Offline: UI/UX Review Feedback (crew package)

Synthesized 2026-09-25 from three independent reviewer passes. Duplicates merged; disagreements noted inline. The author's three non-negotiables (core flow; offline-first, no accounts; MIT attribution + disclaimer) were respected throughout. Copy suggestions stay plain-language, no developer jargon.

## Critical findings

### 1. A download in progress is invisible the moment you look away, and stranded partial downloads are unreclaimable
Progress bars live only under rows on the Browse screen. Navigate to Downloads or About, toggle Resources / Lecture Videos, or scroll the row off-screen, and there is zero indication anything is downloading. The Downloads dashboard lists only *downloaded* courses, so an in-flight download appears nowhere. Worse, Delete is only visible once downloaded: pause a 900 MB video and change your mind, and the partial file sits in limbo, invisible on every screen, occupying storage you cannot free.
- Add a small persistent "active downloads" banner visible on every screen ("Downloading 2 items, 43%"); tapping it jumps to the in-progress rows.
- Show in-progress and paused items at the top of the Downloads dashboard with their progress bars and pause/resume controls.
- Every downloading or paused row gets a cancel control (an x or "Cancel") that deletes the partial file and returns the row to its initial state. Paused items read "Paused: 340 of 900 MB, tap to resume", with partial sizes labeled in storage totals ("1.2 GB downloaded, 300 MB partial").

### 2. The first-run screen shows dead controls above an unexplained blank void
Before any fetch, the Resources / Lecture Videos toggle pair is visible (one looks selected) with an empty list area beneath. A first-time user taps a toggle, nothing meaningful happens, and the screen reads as broken, not ready. It is showing the post-fetch layout in a pre-fetch state.
- Hide the toggle pair and the list until a fetch succeeds. Pre-fetch, that region becomes a first-run intro: one sentence ("Get MIT course videos and readings onto your device, then watch them anywhere, no signal needed.") plus three short steps: 1. Paste a course link below. 2. Tap Get course. 3. Download what you want to watch offline.
- This also frames the offline model up front ("look up while online; everything you download works offline"), which nothing on the current landing screen states.

### 3. Storage is shown in raw bytes, and the storage line does not belong on Browse
"Total storage used: 123456789 bytes" is unreadable; on first run "0 bytes" doubles as clutter that reinforces the empty/broken feeling. All three reviewers flagged this independently.
- Format every size as MB/GB ("1.8 GB") everywhere: Browse rows, per-course rows, dashboard header ("Using 1.8 GB across 3 courses").
- Move the storage total off the Browse screen entirely; the Downloads dashboard owns it. (One reviewer suggested keeping a readable line on Browse; the other two said drop it. Dropping is the majority view and removes duplication.)

### 4. The Downloads dashboard can delete your content but cannot open it
Rows show title, bytes, and Delete, with no way to watch anything. On a train with no signal, the user opens the app to watch offline content, and the screen whose job is "your offline content" only offers deletion. The only route to playback is back through the fetch-first Browse screen, backwards for the primary use case.
- Each downloaded course row gets a primary "Open" (or "Watch") action; row tap itself opens the course's downloaded materials directly, bypassing the fetch flow.
- Make Delete a plain red-tinted text button rather than a heavy button so it cannot be hit by accident.

### 5. Fetch failures and no-network states are a thin status line: invisible and unactionable
On a train, the most likely failure is "no signal right now," and a status line under the Fetch button misses it: no sense of what went wrong, no next step. A bad address and no connection need different remedies.
- Replace the status line with an error panel occupying the results area: what happened in plain words ("Couldn't reach ocw.mit.edu. Check your connection and try again." vs "We couldn't find a course at that address. Double-check it and try again."), one action ("Try Again" re-runs; for a bad address it refocuses the field), and on network failure the offline reminder: "Your downloaded courses still work offline. Open Downloads to watch them."
- Reviewer split on form, agreed on substance: two reviewers want the dedicated panel for visibility; one would accept a prominent inline line under the button. Either way, the thin status line goes.

## Major findings

### 6. "Fetch" is developer vocabulary; the button should say "Get course"
The author removed "slug" but kept "Fetch Course." Everyday users get, load, or find things. Rename the button and the action everywhere ("Getting the course…").

### 7. Up to four buttons per row is a mis-tap farm with undiscoverable states
Download / Pause / View / Delete, plus a Download button whose label "changes with state," forces the user to decode each row. Delete sits next to Watch on a phone screen.
- Collapse to one primary stateful button per row: "Download" → while downloading "Downloading… (tap to pause)" → paused "Resume" → downloaded "Watch"/"Open". Row tap triggers the primary action.
- Row layout: title left, one state button plus one small delete x right, progress bar and size line underneath. Move Delete into a swipe/long-press action on phones if it cannot be a safe secondary tap target.
- Related: pick one verb per media type and use it everywhere: "Watch" for videos, "Open" for documents (currently "View"/"Watch" alternate).

### 8. No file size is shown before downloading; progress bars have no numbers
Lecture videos can be hundreds of MB, yet rows show no size, so the user cannot judge whether a download is worth starting before a dead zone or fits storage. The bar alone, with no percent or "340 of 900 MB," says almost nothing for a large file.
- Each row shows its size next to the title ("Lecture 3, 420 MB"); already-downloaded items get a clear "Downloaded" label rather than relying on button states.
- Under each downloading row: bar plus one text line, "Downloading… 38%, 340 of 900 MB." Keep the bar reasonably thick.

### 9. The Fetch transition is a bare spinner with no status
No text while working, no defined failure modes, and the button apparently stays enabled (double-taps may double-fire).
- While working: inline line under the button reads "Looking up the course…"; button and field disable during fetch. On failure the typed text is preserved and the error appears in that same spot (see finding 5).

### 10. The same instruction is said two and a half times, stacked vertically
Placeholder ("Paste a course link from ocw.mit.edu"), hint line ("Paste the course's web address from ocw.mit.edu and tap Fetch."), and button ("Fetch Course") all say the same thing; the hint is typeset as an italic footnote, the visual language of "ignore me."
- Keep the instruction in the field, delete the separate hint line. Best form: a small real label above the field ("Course link") plus placeholder text inside ("Paste a link from ocw.mit.edu").
- One reviewer also notes the copy hides that a bare course identifier works: placeholder could offer "or type the course's page name" as an alternative.

### 11. "Browse" means two different things
The landing toolbar's "Browse" item leaves the landing screen for the separate "Browse Courses" catalog page, so "Browse" names both the screen you're on and a different screen. Give them distinct names, e.g. "Find a course" (landing, shown selected) | "Course catalog" | "Downloads" | "About". No user should wonder whether they're already where "Browse" goes.

### 12. No confirmation of *what* was fetched
After Fetch, lists populate with no course title header; the user pasted an opaque URL fragment and must trust the list belongs to the right course.
- On success render a header above the tabs: full course title ("Introduction to Algorithms") with a subline like "Here's what we found. Download anything to watch it offline." This is also where a "wrong course? paste a different link" escape hatch lives.

### 13. The Downloads dashboard is static and all-or-nothing
Manual Refresh button at the bottom (why refresh storage by hand?), no live progress, no per-item breakdown, Delete is per-course only, so a watched lecture cannot be deleted without deleting the whole course.
- The dashboard updates live; drop the Refresh button (one reviewer suggested moving it to the header, but live updates make it unnecessary).
- Course rows expand (or tap through) to list items with per-item sizes and individual delete controls.

### 14. Video resume position is saved invisibly
A lecture that starts mid-video with no explanation looks like a bug; the user may have closed it days ago.
- On open with saved progress past ~30 seconds, show a brief banner in the player: "Resuming from 12:34." with a "Start over" option.
- In the lecture list, partially-watched rows show a slim progress track and the button reads "Resume · 12:34"; fully-watched videos get a subtle "Watched" marker.

### 15. The PDF handoff panel reads as an engineering apology
"This PDF opens in your device's own PDF app. The in-app viewer only embeds HTML directly." "Embeds HTML" means nothing to a reader, and the panel never reassures the user the file is safe on their device.
- Flip to the user's action: "This is a PDF file. Tap below to open it in your device's PDF reader." Then the "Open PDF" button, plus one reassurance line: "The file is saved on this device, so it works offline." Lead with a recognizable document icon; drop the dead grey status line at the bottom.
- Handle the handoff's outcomes: on success, brief confirmation ("Opened: check your PDF app"); if no PDF reader is installed, say so plainly ("No PDF reader found on this device. Install one to open this file.") instead of a dead tap.

### 16. Video player error states are unaccounted for
A corrupt or missing downloaded file meets an endless spinner, the worst offline experience, with no recourse and no signal to re-download.
- After a few seconds of failure, the video area swaps the spinner for a centered panel: "This video couldn't be played. The file may be damaged. Try downloading it again when you're back online." with "Back to lectures" always reachable. Name the file, not the app, as the problem.

### 17. About page: required statements are undifferentiated from app info
One flat column means the legally required text has no visual authority and the action buttons compete with it.
- Three headed sections: "About this app" (name, version, one sentence on what it does), "Course content license" (attribution, verbatim), "Not affiliated with MIT" (disclaimer, verbatim, in a bordered/tinted callout). Then the two buttons, clearly separated as actions.
- "Contribute on GitHub" is insider-facing; consider "Help improve this app" with the GitHub reference as a subtitle, plus one inviting line ("Found a problem or want a feature? The app is open source.").

## Minor findings

- Downloads empty state needs the remedy, not just the condition: "Nothing downloaded yet. Find a course on the Browse tab and download its videos to watch them offline." Ideally with a "Browse courses" button that navigates there. (Flagged by two reviewers.)
- The grey "Loading..." line under the video is decoration; use a proper centered loading indicator in the video area that disappears on playback, or remove the line.
- "Resources" is vague as a tab label; consider "Course materials" or "Readings & materials."
- Catalog filter copy "Filter loaded courses by title" leaks mild jargon ("loaded"); use "Search courses by title."
- No bulk controls: starting a lecture series means tapping Download per row. A "Download all videos" / "Pause all" control per list would fit the train use case. (Flagged as minor by one reviewer; cheap to add later.)

## Reviewer disagreements (noted, not resolved)

- **Storage line on Browse:** two reviewers say remove it (dashboard owns storage); one says keep it if made readable. Majority: remove.
- **Error presentation:** two reviewers want a dedicated error panel in the results area; one accepts a prominent inline status line. Agreed: the thin status line is inadequate.
- **Refresh on Downloads:** one reviewer says move it to the header; two say live updates make it unnecessary. Majority: remove, go live.

## The author's open questions (his calls, not the crew's)

- Tab bar vs. the current toolbar-button navigation. (Reviewers flag only that the current top-right items are small tap targets on mobile and read as desktop chrome; if unchanged for v1.0, enlarge touch areas.)
- Pre-filled example course vs. empty field on the landing screen. (Reviewers note: whichever is chosen, the example must look like what the copy asks for; the screenshot's pre-filled identifier fragment contradicts "paste a link." A full example URL the user replaces is friendlier than an empty box; an empty box with placeholder text is cleaner.)
- Stock native look vs. a custom visual theme.
- New question surfaced by review: should the landing screen remember the last-viewed course for returning users? The train user re-opens yesterday's course daily.
- Suggested by one reviewer, flagged only: a small persistent "You're offline" indicator in the header, so fetch failures are preempted rather than explained after the fact.

## Reviewer caveats

- All available screenshots show the pre-change "slug" copy; reviewers evaluated the new wording from text, not rendered. One re-capture after the copy change is worth a glance for line lengths and wrapping.
- Post-fetch states (populated lists, progress rows, PDF panel, video player) were reviewed from written descriptions only; no screenshots of those states exist yet.
