# Store Listing (OCW Offline)

Draft copy for Google Play and Apple App Store listings. Current app identity in code: title **OCW Offline**, application ID `com.owleyebooks.ocwoffline`, version `1.0` (Android `versionCode 1`).

All copy is grounded in verified app behavior: browse the MIT OpenCourseWare catalog (via the MIT Learn public API at `https://api.learn.mit.edu`), download course materials (from `https://ocw.mit.edu`) with resumable downloads into a local SQLite database, view artifacts (PDF hand-off on Android), and play course videos, all offline afterwards. No accounts, no ads, no analytics.

---

## App title options

1. **OCW Offline** *(matches current `ApplicationTitle`; simplest path)*
2. **OCW Offline: MIT Course Viewer**
3. **OpenCourseWare Offline**
4. **OCW Library: Offline MIT Courses**

Recommendation: ship as **OCW Offline** (option 1) to match the in-app title and avoid store/display-name mismatch. Note the trademark consideration in the checklist below.

## Short description (Google Play, ≤ 80 characters)

Current draft (69 characters):

> Browse MIT OpenCourseWare and download full courses to watch offline.

Alternatives:

- `MIT OpenCourseWare courses, downloaded for offline learning.` (60)
- `Download MIT OCW courses: videos and PDFs, ready offline.` (58)

## App Store subtitle (Apple, ≤ 30 characters)

> Offline MIT course library (27)

Alternatives:

- `MIT courses, offline` (19)
- `Download OCW courses` (20)

## Full description (shared base; adapt per store)

```
Learn anywhere: no connection required.

OCW Offline puts MIT OpenCourseWare in your pocket. Browse the full course
catalog, download the courses you want, then watch lectures and read materials
entirely offline: on the train, on a plane, anywhere.

WHAT YOU GET
• Browse and search the MIT OpenCourseWare catalog
• Download full courses: lecture videos, PDFs, and course files
• Resumable downloads that pick up where they left off
• A downloads dashboard showing what's saved and how much space it uses
• An offline library that works with zero connectivity

BUILT FOR LEARNERS
• No account, no sign-up: open the app and start learning
• No ads, no analytics, no tracking
• Your downloads live on your device, under your control

Courses are provided by MIT OpenCourseWare and MIT Learn. OCW Offline is an
independent viewer and is not affiliated with or endorsed by MIT.
```

Google Play allows up to 4000 characters; Apple allows up to 4000. The draft above is well under both limits. Expand with a "PERFECT FOR" section (commuters, students, travelers) if more length is wanted.

### Apple-specific note

Apple's full description should avoid the word "free" claims unless pricing is actually free in all territories, and keyword-stuffing is penalized, so keep the prose natural (the draft above complies).

## Keywords

### Google Play (no keyword field: keywords live in title/short/full description; ensure these terms appear naturally)

mit opencourseware, ocw, offline courses, download lectures, mit courses, free courses, lecture videos, offline learning, course downloader, study offline, education

### Apple App Store (keyword field, 100 characters max, comma-separated, no spaces)

Draft (97 characters):

```
ocw,opencourseware,mit,courses,offline,lectures,download,education,learn,study,university
```

Check character count when editing: `echo -n "kw1,kw2" | wc -c` must stay ≤ 100.

## Category suggestions

| Store | Category | Notes |
|---|---|---|
| Google Play | **Education** | Primary and only sensible fit |
| App Store | **Education** (primary) | Required for an education app |
| App Store | *(no secondary needed)* | Single-category listing is fine |

### Tags / content attributes

- **Google Play:** Education; content rating questionnaire will land at **Everyone** (educational content, no user-generated content, no ads, no in-app purchases). Complete the IARC questionnaire at submission time.
- **App Store:** Age rating expected **4+** (no objectionable content, no user accounts, no web browsing beyond OCW content). Educational apps with no interactive elements typically rate 4+.

## Trademark / attribution notes (resolve before submission)

- "MIT", "MIT OpenCourseWare", and "OCW" are trademarks of the Massachusetts Institute of Technology. The app's description already includes an "independent viewer, not affiliated with or endorsed by MIT" disclaimer, so keep it in the final listing.
- Review MIT OCW's terms of use (https://ocw.mit.edu/terms/) before release to confirm redistribution-via-download of course materials complies; course content is under Creative Commons licenses, but verify per-course license terms hold for an offline-viewer use case.

## Assets still needed (not drafted here)

- App icon: verify the MAUI `Resources/AppIcon` renders correctly at store sizes (512×512 Play, 1024×1024 App Store, no alpha on iOS).
- Screenshots: phone + tablet for both stores (Play requires at least 2; App Store requires 6.5" and 5.5" or current equivalents; check current App Store Connect requirements at submission time).
- Feature graphic (Play, 1024×500).
- Privacy policy URL (from `docs/privacy-policy.md`, must be hosted publicly).
- Support contact / URL for both listings.

---

*Draft notes for the maintainer (remove before publishing):*
- Finalize the app title before first submission: changing it later costs review cycles.
- Confirm the trademark/attribution position on MIT OCW terms of use before release.
- Re-verify the keyword character counts at submission time; store limits change.
