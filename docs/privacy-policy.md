# Privacy Policy (OCW Offline)

**Last updated:** [DATE]
**App:** OCW Offline (`com.mikelab.ocwoffline`)

OCW Offline is an offline viewer for MIT OpenCourseWare course content. It was built to keep your data on your device. This policy describes, in plain language, what the app stores and what leaves your device.

## 1. The short version

- **No accounts.** You never sign in, register, or create a profile.
- **No analytics.** The app contains no analytics SDKs and sends no usage statistics, telemetry, or crash reports to the developer or any third party.
- **No advertising.** There are no ads and no ad-tracking.
- **Your library stays on your device.** Downloaded courses, videos, and PDFs, plus the local database that tracks them, live only in the app's storage on your phone or tablet.
- **Network requests go only to MIT.** The app talks to MIT's public Learn API (`https://api.learn.mit.edu`) for the course catalog and to MIT OpenCourseWare (`https://ocw.mit.edu`) to download course materials. Those requests are necessary for the app to do its job and contain no personal identifiers from the app.

## 2. Data stored locally on your device

The app stores the following **only on your device** (never transmitted to the developer):

| What | Where | Why |
|---|---|---|
| Local course database (`ocw_offline.db3`) | App's private storage (SQLite, via sqlite-net-pcl) | Catalog entries, course metadata, download status and progress |
| Downloaded artifacts (videos, PDFs, other course files) | App's documents folder | Offline access to course materials you chose to download |
| Storage-accounting records | Same SQLite database | Powers the Downloads dashboard (how much space your library uses) |

There is no cloud sync, no remote backup by the app itself, and no way for the developer to see any of this data. Note that standard OS-level behaviors apply: on Android the manifest sets `android:allowBackup="false"`, so the app's data is excluded from Android Auto Backup; on iOS the app's documents folder is exposed to the Files app (`UIFileSharingEnabled`), so files you download are visible in the device's Files app, as intended.

## 3. Data that leaves your device

When you use the app, it makes HTTPS requests to two MIT-operated endpoints:

1. **`https://api.learn.mit.edu`**: MIT Learn's public courses API. Fetches the course catalog, search results, and course metadata.
2. **`https://ocw.mit.edu`**: MIT OpenCourseWare. Downloads the course materials (videos, PDFs, files) you select.

These requests are ordinary content fetches: the app sends the course ID or search query you asked for and receives public course content. The app does not attach any user identifier, account token, or device fingerprint to these requests. Standard network metadata (such as your IP address) is visible to MIT's servers as part of normal web traffic. That is between you and MIT, and MIT's own privacy policies apply to their services.

The app requests two Android permissions, and only these two:

- `INTERNET`: required to reach the MIT Learn API and download course content.
- `ACCESS_NETWORK_STATE`: checks connectivity state around downloads.

No location, camera, microphone, contacts, or storage permissions beyond the app's own folders are requested.

## 4. Accounts

There are none. The app has no sign-in, no registration, no profiles, and no password or credential storage of any kind.

## 5. Analytics and crash reporting

**None.** This was verified against the app's source code and its third-party dependencies (Microsoft.Maui.Controls, sqlite-net-pcl, SQLitePCLRaw, HtmlAgilityPack, CommunityToolkit.Mvvm, CommunityToolkit.Maui.MediaElement). None of them is an analytics or crash-reporting SDK, and the app's own code contains no analytics, telemetry, or crash-reporting logic. If this ever changes, this policy will be updated before any such feature ships.

## 6. Children's privacy

OCW Offline serves all-ages educational content from MIT OpenCourseWare. Because the app collects no personal information from anyone (no accounts, no analytics, no tracking), there is nothing to collect from children either. The app is safe to use without parental supervision from a privacy standpoint, though course content itself is aimed at learners and parents should exercise their own judgment about suitability of specific courses.

## 7. Your control

Everything the app stores is under your control:

- Delete individual downloads from the Downloads dashboard to free space.
- Uninstalling the app removes the local database and all downloaded content from your device (subject to normal OS behavior for files you may have moved out of the app via the iOS Files app).

## 8. Changes to this policy

If the app's data practices change (for example, if analytics were ever added), this policy will be updated and the update will be noted in the release notes before the new version is distributed.

## 9. Contact

Questions about this policy: **[CONTACT (e.g. support email or contact page URL)]**

---

*Draft notes for the maintainer (remove before publishing):*
- Replace `[DATE]` with the publish date of the release this policy ships with.
- Replace `[CONTACT]` with a real support email or contact URL before submitting to either store: both Google Play (Data safety section) and App Store Connect require a contact method and a hosted privacy-policy URL.
- This policy must be hosted at a public URL; add that URL here and in both store listings.
- Re-verify §5 before every release by checking the NuGet/package list for any analytics or crash-reporting SDK.
