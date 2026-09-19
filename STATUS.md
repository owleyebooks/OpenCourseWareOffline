# OpenCoursewareOffline: status

_Last updated: 2026-09-18. Open-source push-ready: git init, CI, About page._

## Open-source push-ready, 2026-09-18

- **Git initialized** at the project root, single initial commit.
  `.gitignore` excludes `bin/`, `obj/`, `.vs/`, `TestResults/`, signing
  files, and `android-tools/` (1.1G). `drop/` and `specs/` are committed
  (1.4M total) as the pristine v43 reference; `drop/` stays read-only by
  convention.
- **MIT LICENSE** (copyright holder "mikelab" is a guess from the package
  ID; correct in `LICENSE` when the legal name is known).
- **README.md / CONTRIBUTING.md**: build/test instructions, OCW
  attribution (CC BY-NC-SA for content, MIT for code), not-affiliated
  disclaimer, prose doctrine, assertion rule; TODO placeholders for the
  GitHub repo URL and hosted privacy-policy URL.
- **CI**: `.github/workflows/android.yml` builds net10.0-android Debug
  and runs the suite via the xUnit console runner on push/PR; `v*` tags
  publish a Release AAB (signed only when the keystore secrets exist).
  `lint.yml` unchanged.
- **Version 1.0**: `ApplicationDisplayVersion` bumped 0.1 to 1.0;
  `ApplicationVersion` stays 1 (nothing ever uploaded to Play).
- **About page** (`Views/AboutPage`, toolbar item on `CoursePage`):
  version, OCW attribution, not-affiliated disclaimer, Privacy Policy and
  Contribute on GitHub buttons. URLs are TODO placeholders in
  `OcwOffline/ExternalLinks.cs`, opened only when they are real https
  addresses; guarded by a 5-case theory.
- **Verify: Android build 0 errors; 233/233 tests green; prose lint clean.**
- **Still the user's**: create the GitHub repo and push; fill the two URL
  placeholders (`OcwOffline/ExternalLinks.cs`, `README.md`); generate the
  release keystore and add the four `ANDROID_*` CI secrets.

## Second push, 2026-09-18

- **MAUI Android compiles for the first time: 0 errors.** `maui-android`
  workload + hand-laid Android SDK in `android-tools/` (excluded from Drive
  sync). Signed debug APK builds. 4 real compile bugs fixed (CommunityToolkit
  usings/param names, `Microsoft.Extensions.Logging.Debug` pin). Build with
  `-p:TargetFrameworks=net10.0-android` (plain `-f` hits NETSDK1178 on Linux).
  Report: `notes/stream-reports/maui-android.md`.
- **Tests: 228/228 green**: 19 new real-SQLite integration tests
  (`CourseDatabaseIntegrationTests.cs`: temp-file DBs, verified through an
  independent `Microsoft.Data.Sqlite` channel). `CourseDatabase` gained an
  optional `IAppPaths?` ctor param (test seam; production unchanged).
  Assertion library migrated to AwesomeAssertions 9.6.0 (`because` on
  multi-aspect guards; xUnit Assert kept for flat verification lists;
  description strings on all multi-case theories).
  Report: `notes/stream-reports/sqlite-integration.md`.
- **MIT Learn API questions resolved:** no server-side search param exists on
  `/api/v1/courses/` (verified against `mitodl/mit-learn` source), and the
  production host `api.learn.mit.edu` was verified live (same DRF envelope and
  serializer schema as the RC host; the v43 Rule 10 block was lifted by user
  directive). The app's client-side filtering is the correct design, no code
  changes. `notes/questions.md` updated with sources. Report:
  `notes/stream-reports/mitlearn-api.md`.
- **v43 process rules archived:** `HANDOFF_v43.md` / `AUDIT_TRAIL_v43.md` moved
  to `archive/v43-process-rules/` with a supersession README, per user
  directive 2026-09-18. Those rules governed the free-Claude round robin and
  do not apply to Muse sessions.
- **Device risks analyzed:** iOS WKWebView local files REAL/unmitigated;
  MediaElement seek race REAL/unmitigated; Android PDF FileProvider likely
  mitigated by design (unconfirmed); progress thrash REAL/partially mitigated.
  Concrete plan: `notes/device-test-plan.md`. Report:
  `notes/stream-reports/device-risks.md`.
- **Store readiness drafted:** `docs/privacy-policy.md` (verified against
  code: no analytics, no auth, network only to MIT hosts),
  `docs/store-listing.md`, `docs/release-checklist.md`. No keys generated.
  Report: `notes/stream-reports/store-readiness.md`.
- **NU1903 (GHSA-2m69-gcr7-jv3q / CVE-2025-6965): bump path identified, not
  yet shipped.** Advisory covers `SQLitePCLRaw.lib.e_sqlite3` (and
  `.android`/`.ios`) ≤ 2.1.11 with no patched versions listed (verified
  2026-09-18). Correction to the earlier note: `lib.e_sqlite3` 2.1.12/2.1.13
  exist and fall outside the vulnerable range (2.1.12 already restores in
  the test project's graph); only `bundle_green` tops out at 2.1.11, which
  is what the app pins, so the app still resolves
  `lib.e_sqlite3.android` 2.1.11. Fix: direct pin on
  `SQLitePCLRaw.lib.e_sqlite3` 2.1.12, to verify on device/CI before
  shipping (mobile TFMs unverifiable on this Linux host).
- **v0.4 prose doctrine applied:** `tools/lint/prose_lint.py` in place
  (347 EMDASH / 0 DODGE / 19 script-flagged DIARY fixed across work/,
  notes/, docs/; ~40 more diary-style comment blocks removed from code
  and csproj, rationale harvested to `notes/decisions.md`). New
  `ProseLintComplianceTests` runs the lint inside `dotnet test`;
  `.github/workflows/lint.yml` runs it in CI. Frozen v43 records
  (`specs/`, `archive/`) and vendored `android-tools/` exempt by documented
  deviation. Assertion library: AwesomeAssertions 9.6.0 adopted 2026-09-18
  (no FluentAssertions usage existed to migrate).

- **Test runner: `dotnet test` still broken in sandbox, console runner
  verified instead (2026-09-18 evening):** the testhost's localhost channel
  to vstest.console receives corrupted data (sandbox egress-proxy
  interference; fails identically post-reboot with proxy vars stripped).
  Verified via the xUnit console runner
  (`dotnet exec /tmp/xunit-console/tools/net6.0/xunit.console.dll
  OcwOffline.Tests.dll` from `bin/Debug/net10.0`):
  **228/228 green, 0 failed, 0 skipped**. `ProseLintComplianceTests`
  now resolves the repo root from the test assembly's directory (it used
  `AppContext.BaseDirectory`, which is the runner's own folder under the
  console runner, outside the repo). The native `e_sqlite3` SQLite provider
  resolves from the NuGet cache only when the runner assembly stays in
  /tmp; do not copy the runner into the test's bin dir.

## Still blocked (needs the user / hardware)

- **iOS:** no Mac (can't restore, compile, or device-test; NETSDK1178).
- **Android emulator/device:** no `/dev/kvm`, no CPU virtualization flags.
  Unblocked by a KVM host, a physical device, or CI with hardware accel.
- **Device verification:** FIRST_BUILD_CHECKLIST items (WKWebView local files,
  video seek race, PDF handoff, progress thrash) need real devices.
- **Shared-library extraction** (kill the 26 file-links): deferred until the
  app compiles somewhere verifiable end-to-end.
- **Store:** signing/provisioning, listing submission, screenshots, privacy
  policy hosting URL + contact: checklists drafted, execution needs the user.

## First pass (v43 fix-up), 2026-09-18

_Previous header: "v43 fix-up complete through item (c); stopping at a clean
green checkpoint." What follows is unchanged from that pass._

## Layout

- `drop/`: **pristine v43 unzip, never modify.** Verified byte-identical
  against `specs/OcwOffline_v43.zip` on 2026-09-18 (an earlier fix-up pass had
  touched it; fully reverted).
- `work/`: active working copy (`OcwOffline/`, `OcwOffline.Tests/`,
  `OcwOffline.slnx`). All implementation happens here.
- `specs/`: original v43 zip (read-only reference). The v43 working papers
  (HANDOFF/AUDIT_TRAIL) moved to `archive/v43-process-rules/` on 2026-09-18;
  their round-robin rules were superseded by user directive and do not apply.
- `notes/assessment-2026-09-18.md`: the 3-question assessment report, as-is.
- `notes/decisions.md`: dated decisions (slnx choice, seam fixes, filter-test
  correction, layout, deferred shared library).

Source of the drop: `OpenCoursewareOfflineMuse` Drive folder.

## Fix-up work done 2026-09-18 (all in `work/`)

- **One-line build fix**: global `<Using Include="Xunit" />` in the test
  csproj → **0 errors** (was 354). Only warnings are the pre-existing NuGet
  ones (NU1903 on SQLitePCLRaw.lib.e_sqlite3, NETSDK1206 RID notice).
- **5 failing tests fixed** (reasoning in `notes/decisions.md`):
  - 4 `CourseViewModel` download-command tests: MAUI's static `MainThread`
    and `FileSystem.AppDataDirectory` throw
    `NotImplementedInReferenceAssemblyException` on the bare-net10.0 test host
    (verified with a standalone probe). Fixed the seam, not the assertions:
    new `IMainThreadDispatcher` + `IAppPaths` in `OcwOffline/Services/`, with
    `MauiMainThreadDispatcher` / `AppPathsProvider` production impls
    registered in `MauiProgram.cs`; tests inject `FakeMainThreadDispatcher`
    (synchronous) and `FakeAppPaths`. Production on-device behavior unchanged.
  - 1 filter test (`FilterEntries_MatchingSubstring`): the test was wrong, not
    the app: `"Algebra"` does not contain the substring `"algo"`. Test now
    searches `"on"` (genuinely matches 2 of 3 sample titles); app filter
    logic untouched.
- **Solution file**: `work/OcwOffline.slnx` (XML format; decision and
  verification in `notes/decisions.md`), includes both projects.
- **Tests: 208/208 green**: `Total: 208, Errors: 0, Failed: 0, Skipped: 0`
  via the xunit v2 console runner. (Note: `dotnet test`/vstest cannot connect
  to its testhost in this sandbox (zero-padded endpoint port); the console
  runner bypasses it. See `notes/env-setup.md`.)

## Earlier verified findings (as-shipped v43, for the record)

- Test project did NOT build as-shipped: 354 errors, all missing `using Xunit;`.
- MAUI app (`net10.0-ios;net10.0-android`) never compiled; SQLite (correct for
  offline mobile); MVVM Toolkit; nullable/file-scoped namespaces/implicit
  usings; Async suffixes; fakes-behind-interfaces.
