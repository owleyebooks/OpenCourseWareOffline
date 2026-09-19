# Decisions

Dated log of key choices and deviations from standards.

## 2026-09-18: Solution format: `.slnx`, not `.sln`

The repo had no solution file. Chose **`.slnx`** (XML solution format) for the new
`drop/OcwOffline.slnx`, per the task default.

**Reasoning:** human-readable, merge-friendly, supported by .NET 9+ SDK and VS
2022 17.12+. The blind-build's classic `.sln` absence was partly a mergeability
hazard in the audit-trail workflow; `.slnx` XML diffs cleanly.

**Verification:** no `dotnet new slnx` template ships in .NET 10.0.401, but the SDK
handles `.slnx` end-to-end: hand-authored `<Solution><Project Path="…"/></Solution>`
XML round-trips through `dotnet sln <file>.slnx list` and `add` (verified
2026-09-18 in a /tmp scratch). No fallback to `.sln` needed. Note: `dotnet sln
<file>.sln migrate` generates an `.slnx` from a classic `.sln`.

## 2026-09-18: Deviations from ~/workspace/standards (legitimate)

Recorded from the 2026-09-18 assessment (see notes/assessment-2026-09-18.md):
- No Api/AppHost split: this is a .NET MAUI mobile app, not a server; standards'
  default solution shape doesn't apply. Projects: OcwOffline (MAUI), OcwOffline.Tests.
- SQLite instead of PostgreSQL: correct for an offline-first mobile app.
- No Aspire AppHost: local-first mobile app; nuget restore at solution level.

## 2026-09-18: v43 fix-up: two new seams in CourseViewModel (IMainThreadDispatcher, IAppPaths)

**Problem:** 4 of the 5 failing tests were download-command tests. Root cause,
verified with a standalone probe: on a bare `net10.0` host (unit tests) MAUI's
static `MainThread.BeginInvokeOnMainThread` and `FileSystem.AppDataDirectory`
both throw `NotImplementedInReferenceAssemblyException`. The fake download
manager faithfully fires `ProgressChanged` (mirroring the real manager), so
`CourseViewModel.OnProgressChanged` threw inside the download `try`, flipping
4 success-path tests to `Failed`. The zip-extraction test had a second,
masked instance of the same class of problem via `AppPaths.Root`.

**Fix (seams, not weakened assertions):** new `IMainThreadDispatcher`
(`MauiMainThreadDispatcher` production impl) and `IAppPaths`
(`AppPathsProvider` production impl) in `OcwOffline/Services/`, both injected
into `CourseViewModel`'s constructor and registered in `MauiProgram.cs`.
Tests inject `FakeMainThreadDispatcher` (synchronous invoke) and `FakeAppPaths`
(inert temp root). Production on-device behavior is unchanged: the impls
delegate to the exact same statics. This follows the project's existing
interfaces-with-fakes convention (IDownloadManager, ICourseDatabase, …).

## 2026-09-18: v43 fix-up: FilterEntries_MatchingSubstring test was wrong, not the app

The 5th failing test expected searching `"Algo"` to return 2 entries including
"Linear Algebra", but `"Algebra"` does not contain the substring `"algo"`.
The app's filter (`Title.Contains(searchText, OrdinalIgnoreCase)`, documented
in the audit trail since the filter's introduction) is correct; the test's
premise was false. Fixed the test, not the app: the search term is now `"on"`,
a genuine case-insensitive substring of exactly two sample titles
("Computati**on**al", "Introducti**on**"), keeping the test's two-match
structure and its "returns only matches" intent.

## 2026-09-18: Layout: drop/ pristine, work/ active; shared-library extraction deferred

Per this chat's bootstrap rules, `drop/` is the pristine v43 unzip and must
never be modified: all fix-up work happens in `work/` (a copy of
`drop/OcwOffline` + `drop/OcwOffline.Tests`; `OcwOffline.slnx` lives at
`work/`). An earlier pass had applied the fix-up directly in `drop/`; it was
fully reverted and `drop/` re-verified byte-identical against
`specs/OcwOffline_v43.zip` (2026-09-18).

The shared-net10.0-library extraction (fix list item d) is deliberately
deferred: with the MAUI app project unverifiable on this machine (no maui
workload; iOS needs a Mac), moving sources between the app and a new library
project cannot be validated end-to-end. The file-link list (now 26 entries)
works and is green; do the extraction when the MAUI app can actually compile
(Mac/CI) so the move is verifiable. a–c plus STATUS.md updates are the
2026-09-18 stopping point.

## 2026-09-18: Second push: MAUI Android compiles for the first time (0 errors)

`dotnet workload install maui-android` (10.0.20/10.0.100; note: plain `maui`
workload is **not supported on Linux**; `maui-android` is the correct one)
plus a hand-laid Android SDK at `android-tools/` (cmdline-tools 13114758,
platform-tools r37.0.1, platforms;android-36, build-tools 36.0.0, Temurin JDK
17.0.20.1). `work/OcwOffline` now compiles for `net10.0-android` with 0
errors; signed debug APK produced. Build with
`dotnet build OcwOffline/OcwOffline.csproj -p:TargetFrameworks=net10.0-android`
(the `-p:` property pin is required: plain `-f net10.0-android` still tries
to resolve the iOS workload and fails with NETSDK1178 on Linux).

4 real compile bugs fixed: `CommunityToolkit.Maui.MediaElement` →
`CommunityToolkit.Maui` using in MauiProgram.cs; named arg
`enableForegroundService:` → `isAndroidForegroundServiceEnabled:`; added
`using CommunityToolkit.Maui.Core;` for `MediaFailedEventArgs` in
VideoPlayerPage.xaml.cs; pinned `Microsoft.Extensions.Logging.Debug` 10.0.11
for `builder.Logging.AddDebug()`.

Emulator: blocked (no /dev/kvm and no vmx/svm CPU flags; QEMU TCG software
emulation not viable). Unblocked by KVM-enabled host, physical device, or CI
with hardware acceleration. iOS: hard block, no Mac (can't even restore on
Linux: NETSDK1178). Details: notes/stream-reports/maui-android.md.

## 2026-09-18: Second push: real SQLite integration tests (19 new, 227/227 green)

The suite was fakes-only for the database. Added
`OcwOffline.Tests/Services/CourseDatabaseIntegrationTests.cs`: 19 tests
driving the real `CourseDatabase` (sqlite-net-pcl) against temp-file SQLite
databases, verified through an independent `Microsoft.Data.Sqlite` channel
(test csproj only): schema/indexes, CRUD round-trips, download-state
persistence and resume across fresh instances, reconciliation lookups,
`UpdateWatchProgress`, `GetTotalBytesDownloaded`, cascade-delete scoping, and
8-way concurrent first-access racing the schema guard.

One minimal production change: `CourseDatabase` hardcoded its path via static
`AppPaths.Root` (throws on the bare net10.0 test host), so it now takes an
optional `IAppPaths? paths = null` constructor parameter: production DI
behavior unchanged, following the v43 `IAppPaths` seam pattern.

Deviation from standards/testing.md: testing.md wants PostgreSQL/Testcontainers
integration tests; this project's data layer is SQLite by product design
(offline-first MAUI app), so integration tests target real temp-file SQLite
instead. Details: notes/stream-reports/sqlite-integration.md.

## 2026-09-18: NU1903 (GHSA-2m69-gcr7-jv3q / CVE-2025-6965): corrected finding

The high-severity advisory on `SQLitePCLRaw.lib.e_sqlite3` (and the
`.android` / `.ios` variants) covers versions ≤ 2.1.11 with no patched
versions listed (GitHub Advisory Database, verified 2026-09-18). Correction
to the earlier version of this entry: `lib.e_sqlite3` 2.1.12 and 2.1.13 DO
exist on nuget.org (2.1.12 already restores in the test project's graph via
`Microsoft.Data.Sqlite` → `bundle_e_sqlite3` 2.1.12) and fall outside the
advisory's vulnerable range. What does not exist is `bundle_green` 2.1.12:
`bundle_green` tops out at 2.1.11, and that is the package the app pulls
(direct pin, `OcwOffline.csproj`), so the app still resolves
`lib.e_sqlite3.android` 2.1.11, inside the vulnerable range. The bump path is
a direct pin on `SQLitePCLRaw.lib.e_sqlite3` 2.1.12; the app's mobile TFMs
cannot be verified on this Linux host, so verify on device or CI before
shipping the bump. CVE-2025-6965 is a SQLite numeric-truncation issue
(CWE-197), attack complexity high; accepted risk for a local read-mostly
course DB until the bump is verified.

## 2026-09-18: MIT Learn API: no server-side search param exists

The two open API questions (archive/v43-process-rules/HANDOFF_v43.md rows 37–38), researched
against the real `mitodl/mit-learn` backend source:
1. Production host question: categorically blocked per spec rule, not touched.
2. Server-side search/text param on `/api/v1/courses/`: **does not exist.**
   The viewset uses a custom `MultipleOptionsFilterBackend` with exactly 14
   filters (`free`, `department`, `resource_type`, `offered_by`, `platform`,
   `level`, `topic`, `course_feature`, `readable_id`, `resource_id`, `sortby`,
   `delivery`, `certification_type`, `resource_type_group`); real text search
   lives in a separate OpenSearch-backed service. Bonus: DRF limit/offset
   pagination (default 10, max 100), anonymous GET needs no auth,
   `platform=ocw` valid; all matching the app's existing usage, so **no code
   changes were needed**. `notes/questions.md` updated with findings and
   sources. Consequence: `CatalogViewModel.FilterEntries` client-side title
   filtering remains the correct design. Details:
   notes/stream-reports/mitlearn-api.md.

## 2026-09-18: Device risks analyzed; device-test plan written

Static analysis of the four FIRST_BUILD_CHECKLIST risks (archive/v43-process-rules/HANDOFF_v43.md,
docs/FIRST_BUILD_CHECKLIST.md) against `work/OcwOffline`, no code changed:
1. iOS WKWebView local files: REAL, unmitigated (absolute `file://` URI in
   `ArtifactViewerPage.xaml.cs:LoadArtifact`, no folder read-access scope, no
   custom iOS WebViewHandler). Blocked on iOS hardware.
2. MediaElement seek-on-open race: REAL, unmitigated
   (`VideoPlayerPage.xaml.cs:OnMediaOpened` seeks immediately, no decoder-
   readiness gate).
3. Android PDF FileProvider: likely mitigated by design (cache/sharing-root
   matches Essentials' bundled fileprovider paths; `<queries>` block added in
   v21); do NOT preemptively add `provider_paths.xml`: only if the device
   test throws.
4. Progress-update thrash: REAL, partially mitigated (100 ms throttle +
   main-thread dispatch); the 100→250/500 ms bump stays a device-measured
   fallback.
Concrete plan: notes/device-test-plan.md. Details:
notes/stream-reports/device-risks.md.

## 2026-09-18: Store-readiness drafts written (docs only)

`docs/privacy-policy.md` (verified against code: no analytics/crash SDKs, no
auth, network only to api.learn.mit.edu + ocw.mit.edu, local SQLite +
downloads; `[CONTACT]`/`[DATE]` placeholders), `docs/store-listing.md`
(title options, ≤80-char short description, keyword lists, both stores),
`docs/release-checklist.md` (Android keystore/Play signing/AAB and iOS
provisioning as to-be-executed checklists). No keys generated. Details:
notes/stream-reports/store-readiness.md.

## 2026-09-18: v0.4 prose doctrine applied (lint, assertion library, compliance test, CI)

Applied the v0.4 comment/prose doctrine (standards/dotnet-conventions.md #16-18,
testing.md #11-13) to this project.

- Canonical lint script copied to `tools/lint/prose_lint.py` (chmod +x).
- Project-local deviations from the canonical script (this project's copy only):
  - `android-tools/` added to SKIP_DIRS: vendored third-party toolchain
    (JDK/Android SDK); its `legal/*.md` files are not ours to edit.
  - `specs/AUDIT_TRAIL_v43.md`, `specs/HANDOFF_v43.md`, and their copies under
    `archive/v43-process-rules/` added to ARCHIVE_EXEMPT (skipped by all
    rules): the v43 experiment's own frozen records; rewriting 183KB of
    turn-by-turn history would falsify the record.
- Violation counts fixed: EMDASH 347, DODGE 0, DIARY 19 script-flagged
  (plus ~40 diary-style comment blocks/narrations removed from csproj and
  .cs files beyond the script's heuristic: version-history comments,
  "See AUDIT_TRAIL vXX" pointers, "this round"/"since vXX" narration).
  11 of the EMDASH fixes were in string literals (5 user-facing status
  strings in the app, 4 fake exception messages, 2 test expectations),
  reworded in lockstep so tests still match app output.
- Assertion library: no FluentAssertions usage anywhere in the solution;
  nothing to migrate. Tests use xUnit Assert only.
- Standards-compliance test added: `OcwOffline.Tests/ProseLintComplianceTests.cs`
  runs `tools/lint/prose_lint.py` over the repo root via Process and asserts
  exit code 0 (skips gracefully with a clear message if python3 is missing).
- CI: `.github/workflows/lint.yml` (checkout, setup-python, run script).
- Diary comments removed from code/csproj; still-relevant rationale harvested
  into dated entries here instead of living inline.

## 2026-09-18: v43 process rules archived; Rule 10 block lifted

User directive: the v43 PROCESS/HANDOFF/AUDIT rules governed the 43-turn
free-Claude round robin (tiny token budgets) and do not apply to Muse
sessions, which have a much larger runway.

- `specs/HANDOFF_v43.md` and `specs/AUDIT_TRAIL_v43.md` moved to
  `archive/v43-process-rules/` with a supersession README. `drop/` untouched.
  Path references in `notes/` updated to the archive location.
- PROCESS Rule 10's categorical block on checking `api.learn.mit.edu` is
  lifted. Follow-through done the same day: production host fetched live,
  confirmed serving the same `/api/v1/courses/` DRF envelope and serializer
  schema as the RC host (count 2586 vs RC 2903; delta is staging snapshot vs
  production published set, irrelevant to the paginating client). Recorded in
  `notes/questions.md` Q1. No code change needed; `OcwCatalogService` already
  targets production.

## 2026-09-18: Rationale harvested from csproj comments (v0.4 prose pass)

The v0.4 prose pass deleted diary-style comments from both csproj files.
Still-relevant rationale, restated in the present tense:

1. The Tests project stays on the xUnit v2 trio (Microsoft.NET.Test.Sdk
   17.12.0, xunit 2.9.2, xunit.runner.visualstudio 2.8.2): the SDK still
   supports classic VSTest mode by default.
2. The Tests project file-links app sources instead of using a
   ProjectReference: the app project has no bare-net10.0 TFM to resolve a
   reference against.
3. The Tests project's package versions match the app's pins.
   CommunityToolkit.Mvvm and sqlite-net-pcl target netstandard/net-generic,
   not the MAUI mobile TFMs, so the bare-net10.0 test project references
   them directly.
4. Microsoft.Maui.Controls.Core (10.0.80) is referenced instead of the
   Microsoft.Maui.Controls meta-package: only Controls.Core ships a real
   bare-net10.0 asset. Version matches the app's pin.
5. Microsoft.Maui.Essentials (10.0.80) is needed for the FileSystem call in
   the file-linked AppPaths.cs, which Controls.Core does not provide.
6. Microsoft.Data.Sqlite is test-only: integration tests drive the real
   file-linked CourseDatabase against a temp-file SQLite database, then
   verify through this independent ADO.NET channel as cross-library proof
   that rows land in the file. The app itself stays on sqlite-net-pcl.
7. Test-project usings are explicit, not implicit: MAUI's auto-injected
   usings are gated on $(UseMaui)=='true', which the plain test project
   does not set. ApplicationModel provides the MainThread used by the
   file-linked CourseViewModel.
8. The global Using Xunit exists because most test files use
   [Fact]/[Theory] without declaring using Xunit. It composes with files
   that do declare it, since duplicate using of the same namespace is
   legal C#.
9. MAUI's static MainThread and FileSystem.AppDataDirectory throw
   NotImplementedInReferenceAssemblyException on a bare net10.0 host, so
   the IMainThreadDispatcher and IAppPaths seams exist with Fakes test
   doubles. The Maui production implementations are linked for compile;
   tests never execute the MAUI calls.
10. Integration tests construct the real CourseDatabase with a per-test
    temp IAppPaths root so the static AppPaths.Root (which throws on a
    bare net10.0 host) is never touched. The parameter defaults to null
    in production.
11. The app project pins the Android min API at 24.0 rather than 21.0: 24
    matches the .NET 10 shipped template default and avoids Java
    default-interface-method desugaring crashes. API 21 still builds under
    .NET 10 but is slated for removal in .NET 11.
12. CommunityToolkit.Maui.MediaElement stays at 10.0.0: nuget.org's
    canonical package page lists it as current, while GitHub's release
    feed shows higher versions under a versioning scheme that does not
    match the nuget listing. Do not bump from the GitHub feed without
    reconciling the scheme.
13. Microsoft.Extensions.Logging.Debug provides
    builder.Logging.AddDebug() used in MauiProgram's #if DEBUG block (the
    same package the stock MAUI template references).
## 2026-09-18: Assertion migration: AwesomeAssertions 9.6.0 adopted for multi-aspect guards

Targeted migration, user-approved ("now or never"). Only `work/` touched;
`drop/` and the Drive original untouched.

- AwesomeAssertions 9.6.0 added to `OcwOffline.Tests` (latest stable on
  nuget.org 2026-09-18), with a global `Using` alongside the existing
  global Xunit using. Free FluentAssertions fork; FluentAssertions itself
  is paid now, and xUnit Assert has no `because`.
- Conversion rule: AwesomeAssertions `because` only where multiple checks
  guard distinct aspects or regression boundaries under shared setup (the
  valid-last-names-found vs first-names-not-queried shape). Flat
  field-by-field persistence checks, JSON shape checks, default-value
  lists, and other self-evident verification lists stay on xUnit Assert;
  `because` there would be noise.
- Converted tests (test names unchanged):
  - `Services/CourseDatabaseIntegrationTests.cs`:
    `DeleteCourseDataAsync_RemovesOnlyTargetCourseRows` (survivor-row
    checks now carry `because`; the "other course is untouched" comment
    removed, its intent moved into the assertions).
  - `Services/FakeCourseDatabaseTests.cs`:
    `DeleteCourseDataAsync_RemovesCourseArtifactsAndLectures_LeavesOtherCoursesIntact`.
  - `ViewModels/DownloadsDashboardViewModelCommandTests.cs`:
    `DeleteCourseAsync_RemovesRowAndUpdatesTotal_LeavesOtherCourseIntact`
    and `RefreshAsync_SkipsCoursesWithoutExistingDownloads`.
  - `ViewModels/CourseViewModelFetchTests.cs`:
    `FetchAsync_Success_AddsCourseZipArtifactAndLecture` (the
    "course zip + syllabus" comment removed, intent in assertions) and
    `FetchAsync_ReFetch_UpdatesExistingArtifactInPlaceRatherThanDuplicating`.
  - `ViewModels/CourseViewModelCommandTests.cs`:
    `DeleteArtifactAsync_Completed_DeletesFileAndResetsFields`,
    `DeleteLectureAsync_Completed_DeletesFileAndResetsFields`,
    `DeleteArtifactAsync_ExtractedZip_DeletesExtractedContents` (flat
    model-reset field lists stay xUnit; storage-deletion and
    extraction-state checks use `because`).
  - `ViewModels/CourseViewModelDownloadCommandTests.cs`:
    `DownloadArtifactAsync_ZipType_ExtractsAndSetsIsExtracted` and
    `DownloadArtifactAsync_Success_NonZip_SetsCompletedAndLocalFilePath`
    (the non-zip must-stay-unextracted guard).
  - `ViewModels/CatalogViewModelLoadMoreTests.cs`:
    `LoadMoreAsync_Success_AppendsEntriesAndAdvancesOffset`.
- Deliberately left on xUnit: `UpsertCourseAsync_NewCourse_PersistsAllColumnsToSqliteFile`
  (flat per-column persistence list), JSON deserialization shape tests,
  default-property-value tests, and the flat reset-field portions of the
  delete tests above.
- Theory descriptions: all 14 multi-case theories gained a `string
  description` first parameter supplied as the first `InlineData` value
  (method names unchanged), so each case's reason shows in Test Explorer:
  `StatusIsInProgressConverterTests` (5), `DownloadButtonTextConverterTests`
  (2+4), `StatusIsActiveConverterTests` (2+4), `StringNotEmptyConverterTests`
  (2), `InvertedBoolConverterTests` (2+2), `ArtifactTests` (2+2),
  `OcwScraperServiceTests.ParseSize_KnownUnits_ConvertsToBytes` (4),
  `OcwCatalogServiceSlugFromUrlTests` null/whitespace (3),
  `ArtifactViewingLogicTests` (2+2). Bodies ignore the parameter; the
  display name is the purpose.
- API pitfall found during conversion: `collection.Should().Equal(x,
  "because")` silently binds to the `params T[]` overload (the reason
  becomes a second expected element) instead of the
  `(IEnumerable<T>, string because, ...)` overload. Both affected call
  sites now pass an explicit array: `Equal(new[] { ... }, "because")`.
  The int overload fails loudly (CS1503); the string overload does not.
- Result: build 0 errors; full suite 228/228 green, 0 failed, 0 skipped,
  via the xUnit console runner (`dotnet test`/vstest still broken in the
  sandbox). Prose lint clean.

## 2026-09-18: ProseLintComplianceTests: repo root resolved from test assembly location

The compliance test walked up from `AppContext.BaseDirectory` looking for
`tools/lint/prose_lint.py`. Under the xUnit console runner (the only
working runner in this sandbox) the base directory is the runner's own
folder, outside the repo, so the test failed with "Could not locate repo
root" even though the lint itself is clean. It now walks up from the test
assembly's directory instead, which is correct under vstest, the console
runner, and CI alike. No behavior change to the lint itself.

## 2026-09-18: Git init for open-source push

Repo initialized at the project root with a single initial commit once
push-ready. `.gitignore` excludes `bin/`, `obj/`, `.vs/`, `TestResults/`,
signing files (`*.keystore`, `*.p12`, `*.mobileprovision`), and
`android-tools/` (1.1G hand-laid SDK+JDK, also excluded from the Drive
sync; recreated per `notes/env-setup.md`). `drop/` (984K) and `specs/`
(440K) are committed: small enough for git and they make the repo
self-contained as the pristine v43 reference. `drop/` stays read-only by
convention (never modify).

## 2026-09-18: LICENSE copyright holder is a guess

MIT license at repo root names copyright holder "mikelab", taken from the
existing package ID `com.mikelab.ocwoffline`. The user's legal name is not
on file. Correct the holder in `LICENSE` when the real name is known; it
is a one-line change.

## 2026-09-18: Version bumped to 1.0 for first release

`ApplicationDisplayVersion` 0.1 to 1.0 in `OcwOffline.csproj`.
`ApplicationVersion` (Android versionCode) stays at 1: it only needs to
increase relative to what Play has seen, and nothing was ever uploaded.

## 2026-09-18: About page with placeholder external links

New `Views/AboutPage` reachable from a toolbar item on `CoursePage` (the
app's root page), following the existing `Func<TPage>` factory navigation
pattern; registered transient in `MauiProgram` like the other pages. No
viewmodel: the content is static text plus two link buttons. Contents:
app name + runtime version, MIT OCW attribution (CC BY-NC-SA), the
not-affiliated-with-MIT disclaimer, Privacy Policy and Contribute on
GitHub buttons. Both URLs live in `OcwOffline/ExternalLinks.cs` as
clearly-marked TODO placeholder constants; the page only opens a link
when `ExternalLinks.IsConfigured` says it is a real https address,
otherwise it shows a "not set up yet" alert. `ExternalLinks` is
file-linked into the test project with a small theory covering the guard.
The placeholder strings must be replaced when the GitHub repo exists and
the privacy policy is hosted (also noted as TODO in `README.md`).

## 2026-09-18: CI android.yml design

`.github/workflows/android.yml` (existing `lint.yml` untouched): on
push/PR it builds `net10.0-android` Debug and runs the full suite via the
xUnit console runner installed from the `xunit.runner.console` 2.9.2
package (parity with the documented local path in `notes/env-setup.md`;
`dotnet test`/vstest is broken only in the maintainer's sandbox).
Version tags (`v*`) additionally publish a Release AAB and upload it as
an artifact. Signing is conditional: the AAB is signed only when the
`ANDROID_KEYSTORE_BASE64`, `ANDROID_KEY_ALIAS`, `ANDROID_STORE_PASS`,
and `ANDROID_KEY_PASS` secrets exist, otherwise it builds unsigned.
Secrets documented in the workflow header and in
`docs/release-checklist.md` A1.

## 2026-09-18: Package ID changed to com.owleyebooks.ocwoffline

The `mikelab` segment was AI-invented in the v43 drop, never chosen by the
user. The user (Mike) chose `com.owleyebooks.ocwoffline` as the permanent
publisher identity. Changed `ApplicationId` in `work/OcwOffline/OcwOffline.csproj`
(which flows to both the Android applicationId and the iOS bundle ID) and
updated all doc references (`docs/privacy-policy.md`, `docs/store-listing.md`,
`docs/release-checklist.md`). Verified in the built APK via aapt:
`package: name='com.owleyebooks.ocwoffline' versionCode='1' versionName='1.0'`.
Historical notes entries still mention the old ID; they are dated records and
were left as-is. The LICENSE copyright holder still reads "mikelab" and needs
the user's correction to their legal name or brand. The ID is now locked in:
it cannot change after the first store upload.
