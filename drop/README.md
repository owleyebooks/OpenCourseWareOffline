# OcwOffline

*(working title — display name not finalized; the code namespace stays
`OcwOffline` regardless of what this project ends up being called)*

Browse, download, and watch MIT OpenCourseWare content offline — with
progress tracking — on your phone.

## Why this exists

I wanted to actually finish courses that interested me — on my commute,
wherever signal happens to be unreliable that day. MIT OpenCourseWare has
the material — real MIT courses, free, including lecture video — but
there's no good way to grab a course before you lose signal and pick up
watching where you left off once you're back online. That's the whole idea:
browse a catalog, pick a course, download it while you have signal, and
watch it — wherever — with the app tracking what you've already seen.

## What it does

- **Browse** MIT OpenCourseWare's course catalog and find something to learn.
- **Download** a course's video lectures and supporting materials (PDFs, etc.)
  for offline use.
- **Watch** downloaded video and view downloaded documents entirely offline,
  no connection required.
- **Track progress** — resume a lecture where you left off, see what's
  downloaded, in progress, or done across your whole course library.

## What it deliberately doesn't do

- **No YouTube extraction.** Not a general-purpose downloader.
- **No support for MITx, xPRO, Bootcamps, or other MIT online-course
  platforms.** See [Why OCW only](#why-ocw-only) below.
- **No content redistribution features.** This is a personal offline cache,
  not a way to share or re-host course material with other people.
- **No bundled course content.** This repository ships code only — see
  [License](#license).

## Why OCW only

MIT publishes several very different things under the broader "MIT online
learning" umbrella, and it's worth being precise about which one this app
touches:

- **MIT OpenCourseWare (OCW)** — MIT's own raw teaching materials, published
  for free, self-directed study. No enrollment, no login, no certificate.
  Critically, OCW is openly licensed under **Creative Commons
  (BY-NC-SA)** specifically so it can be downloaded, saved, and reused —
  that's not a loophole, it's the stated purpose of the license.
- **MITx, MIT xPRO, MIT Bootcamps, Sloan Executive Education, the Open
  Learning Library**, etc. — real course *products*: graded, sometimes
  cohort-paced, sometimes paid, sometimes credentialed. They're built for
  enrolled learners taking a course, not for open bulk redistribution, and
  none of them carry OCW's CC license.

This app only ever downloads from OCW, and only ever from `ocw.mit.edu`
itself. That's not an arbitrary scope limit — it's the boundary of what's
actually licensed for this kind of use. Supporting the other platforms would
mean building around logins, graded courseware, and content that isn't
openly licensed for offline redistribution, which is a different project
with a different legal footing.

One other MIT service is involved, indirectly: **MIT Learn**, a newer
discovery/search site that indexes *all* of MIT's course offerings (OCW
included). It's used here purely as a course-lookup data source for the
Browse screen — it hands back titles and slugs so you can find a course
without already knowing its URL. Every actual download always goes straight
to `ocw.mit.edu`, regardless of what Browse used to find it, so MIT Learn
never touches the part of the app that pulls or stores content. See
[docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) for the technical detail.

## How this compares to what else is out there

A few other things exist in this space:

- **MIT's own "OCW To Go"** — MIT OpenCourseWare's own site lists this as
  supporting offline use on mobile devices for select courses, launched
  alongside MIT Learn. Not much public detail is available (no dedicated
  page or App Store/Play Store listing found), so it's genuinely unclear
  whether this is a native app, a browser-based "save for offline" feature,
  or something else — and whether it includes progress tracking. Worth
  checking directly on ocw.mit.edu before assuming this project is filling
  a gap MIT hasn't already addressed.
- **[mit-ocw-offline](https://github.com/)** — a self-hosted tool that
  downloads the OCW catalog and serves it through a local web UI via Docker.
  Closest in spirit, but it's a home-server tool you run on a machine, not
  something that lives on your phone with you.
- **mit-ocw-dl** — a command-line tool that downloads one course's videos at
  a time. No catalog browsing, no offline playback UI, no progress tracking.
- **OpenCourseWare Companion** — an unofficial third-party Android app for
  browsing and downloading OCW content. Appears to be a dormant hobby
  project (last update found dates to 2017, no App Store/Play Store listing
  found, no signs of active use); Android only, no offline-playback or
  progress-tracking feature set, and no roadmap found for iOS.

Aside from MIT's own unconfirmed offering, nothing found combines browse,
download, offline playback, and progress tracking in one mobile app, which
is the gap this project is aiming at.

(Checked as of mid-2026 — a fair snapshot, not a guarantee nothing new has
shown up since.)

## Status

Actively in development, aiming to be as complete and correct as possible
before a first real build and release.

## Architecture

See [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) for how the app is put
together: services, data flow, the MIT Learn integration in detail, and a
diagram of the pieces.

## Content and licensing

This repository contains **code only** — no course videos, PDFs, or other
OCW content are bundled or committed here. The app downloads content
directly from `ocw.mit.edu` at runtime, into local storage on your own
device, under the terms of OCW's own Creative Commons license.

- **This code** is licensed under the [MIT License](LICENSE).
- **Course content** you download through the app remains MIT
  OpenCourseWare's own Creative Commons–licensed material, governed by
  [OCW's terms](https://ocw.mit.edu/terms/), not by this project's license.

## Contributing

This is a personal project, shared in case it's useful to someone else with
the same problem. Issues and pull requests are welcome, but there's no
guaranteed response time or roadmap commitment — this is maintained on a
best-effort basis alongside everything else in life.
