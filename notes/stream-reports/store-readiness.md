# Stream 5 report: store-readiness drafts (2026-09-18)

## What was drafted

Three documents created in `~/workspace/opencoursewareoffline/docs/` (new dir):

- [docs/privacy-policy.md](sandbox://workspace/opencoursewareoffline/docs/privacy-policy.md): tailored privacy policy
- [docs/store-listing.md](sandbox://workspace/opencoursewareoffline/docs/store-listing.md): title options, short description (69 chars, ≤80 verified), App Store subtitle (26 chars, ≤30), full description, keyword lists (Apple keyword string 89 chars, ≤100 verified), Education category for both stores, trademark notes
- [docs/release-checklist.md](sandbox://workspace/opencoursewareoffline/docs/release-checklist.md): Android (keystore/keytool documented as commands only, Play App Signing, versionCode/versionName, AAB build) and iOS (Developer program, certs/profiles, App Store Connect, versioning) checklists; nothing executed

Constraints honored: docs only, no code touched, `drop/`, `notes/decisions.md`, and `STATUS.md` untouched, no keys/certificates generated.

## Key facts verified from source (work/OcwOffline)

- **No analytics / crash reporting:** only 6 NuGet packages (MAUI Controls, sqlite-net-pcl, SQLitePCLRaw, HtmlAgilityPack, CommunityToolkit.Mvvm, CommunityToolkit.Maui.MediaElement). A grep for appcenter/firebase/crashlytics/sentry/telemetry/analytics returned zero hits. The privacy policy's §5 states this was verified against the dependency list.
- **No accounts:** no login/register/OAuth/password code anywhere (grep hits were false positives like "accounts for" / `[Register("AppDelegate")]`).
- **Network leaves device only to MIT:** `https://api.learn.mit.edu` (OcwCatalogService, catalog/search) and `https://ocw.mit.edu` (OcwScraperService + DownloadManager, course downloads). No user identifiers attached.
- **Local storage:** SQLite `ocw_offline.db3` at `AppPaths.Root` (iOS Documents dir via NSFileManager; Android AppDataDirectory). Downloaded artifacts (videos/PDFs) stored on device; storage accounting in the same DB feeds the Downloads dashboard. No MAUI `Preferences` usage found.
- **Permissions:** Android manifest requests only `INTERNET` and `ACCESS_NETWORK_STATE`, plus `allowBackup=false` and a PDF-viewer `<queries>` intent. iOS Info.plist: `UIFileSharingEnabled` (documents visible in Files app), `LSSupportsOpeningDocumentsInPlace`, audio background mode (MediaElement requirement).
- **App identity:** title `OCW Offline`, ID `com.mikelab.ocwoffline`, display version `0.1`, Android versionCode `1`. Features confirmed: catalog page, course page, resumable downloads (HTTP Range logic in DownloadManager), downloads dashboard, artifact viewer (Android PDF hand-off via Launcher), video player page.
- **Features for store copy:** all description claims map 1:1 to verified code.

## Placeholders the user must fill in before submission

1. **Privacy policy:** `[DATE]` (last-updated) and `[CONTACT]` (support email/URL), required; policy must be hosted at a public URL before either store form is submitted.
2. **Store listing:** final app-title choice (recommendation: "OCW Offline" to match in-app title); resolve the MIT/OCW trademark position: check https://ocw.mit.edu/terms/ for redistribution-via-download compliance before release (noted in the doc).
3. **Release checklist:** all `[PLACEHOLDER]` values (keystore path, alias, passwords via Secure Vault, never the repo); first public release should bump to display version `1.0` and a versionCode higher than anything Play has seen.
4. **Assets not drafted:** store screenshots, Play feature graphic (1024×500), icon size verification (512 Play / 1024 iOS no-alpha).

## Consistency notes

- All three docs agree: no data collected, no accounts, no analytics → both stores' Data safety / App Privacy answers are "no data collected".
- Expected ratings: Play **Everyone**, App Store **4+** (documented as questionnaire outcomes to confirm at submission, not asserted as final).
- Privacy policy carries a re-verification note (§5 + pre-release gate C) to re-check the package list before every release.
