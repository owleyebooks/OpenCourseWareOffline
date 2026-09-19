# Stream 1 Report: MAUI Android compile (OpenCoursewareOffline)

Date: 2026-09-18. Author: Stream 1 subagent. Status: **DONE: app compiles for net10.0-android, signed APK produced, test suite green (227/227).**

## Outcome

`work/OcwOffline` now builds clean for `net10.0-android`: **0 errors**, only the pre-existing NU1903 warning (see below). Signed debug APK produced at:
- `work/OcwOffline/bin/Debug/net10.0-android/com.mikelab.ocwoffline-Signed.apk` (21.3 MB)
- unsigned: `com.mikelab.ocwoffline.apk`

Build command used (Linux; iOS workload packs don't exist on this platform, so the
TFM is pinned at the command line (**no csproj change**)):
```
dotnet build OcwOffline/OcwOffline.csproj -p:TargetFrameworks=net10.0-android
```
with `ANDROID_HOME`/`ANDROID_SDK_ROOT`/`JAVA_HOME` set (see Environment).

## Workload / SDK versions installed

- .NET SDK 10.0.401 (pre-existing at `/home/hatch/workspace/ranksandbeans/.dotnet`)
- `dotnet workload install maui-android` → **maui-android 10.0.20/10.0.100, SDK 10.0.400** (installed OK).
  Note: `dotnet workload install maui` is **not supported on Linux** ("Workload ID maui
  isn't supported on this platform"); `maui-android` is the correct Linux workload.
- .NET Android pack: `Microsoft.Android.Sdk.Linux` **36.1.69** (so `net10.0-android`
  targets **API 36**; the csproj's `SupportedOSPlatformVersion 24.0` is the *minimum*,
  not the target (verified by the successful build against `platforms;android-36`).
- Android SDK (fresh, at `~/workspace/opencoursewareoffline/android-tools/`):
  - cmdline-tools `13114758` (from dl.google.com)
  - `platform-tools` r37.0.1, `platforms;android-36` (platform-36_r02), `build-tools;36.0.0`
  - Eclipse Temurin **JDK 17.0.20.1+1** (a JRE is not enough; the Android build
    requires `jar`; see gotchas). Kept at `android-tools/jdk17/jdk-17.0.20.1+1`.
- Original zips retained in `android-tools/dl/` (~140 MB) for reproducibility.

## Compile errors found and fixed (all real bugs)

1. **`MauiProgram.cs`: wrong namespace for the MediaElement init.**
   `using CommunityToolkit.Maui.MediaElement;` → **CS0234**; in the standalone
   `CommunityToolkit.Maui.MediaElement` 10.0.0 package the `AppBuilderExtensions`
   class lives in namespace `CommunityToolkit.Maui`. Fixed by changing the using
   (verified against the package's own `CommunityToolkit.Maui.MediaElement.xml` docs).
2. **`MauiProgram.cs`: wrong named argument.**
   `.UseMauiCommunityToolkitMediaElement(enableForegroundService: false)` would fail
   CS1739; the parameter is actually named **`isAndroidForegroundServiceEnabled`**
   (per the package XML docs). Renamed the argument and updated the adjacent comment.
3. **`Views/VideoPlayerPage.xaml.cs`: `MediaFailedEventArgs` in wrong namespace.**
   CS0246 with `using CommunityToolkit.Maui.Views;`: the type lives in
   **`CommunityToolkit.Maui.Core`** in v10. Added the using.
4. **`OcwOffline.csproj`: missing package.**
   `builder.Logging.AddDebug()` (CS1061): the stock-MAUI-template package
   `Microsoft.Extensions.Logging.Debug` was never referenced. Added pinned
   `<PackageReference Include="Microsoft.Extensions.Logging.Debug" Version="10.0.11" />`
   (10.0.11 already present in the local NuGet cache; explicit pin per standards).

Files changed (3): `work/OcwOffline/MauiProgram.cs`, `work/OcwOffline/Views/VideoPlayerPage.xaml.cs`,
`work/OcwOffline/OcwOffline.csproj`. No changes to `notes/decisions.md` or `STATUS.md`
(coordinator owns those). `drop/` untouched.

Remaining warning (pre-existing, not introduced here): NU1903: `SQLitePCLRaw.lib.e_sqlite3.android`
2.1.11 has a known high-severity vulnerability (GHSA-2m69-gcr7-jv3q). Present since the
first restore; left for the coordinator to disposition (pin bump vs. suppression decision).

## Test results after changes

- `work/OcwOffline.Tests` rebuilt: 0 warnings, 0 errors.
- Suite run via the xunit v2 console runner (`/tmp/xunit-console/tools/net6.0/xunit.console.dll`)
  with `DOTNET_ROLL_FORWARD=LatestMajor` (the net6.0 runner needs the missing .NET 6 runtime
  otherwise (roll-forward to the net10 runtime works)): **Total: 227, Errors: 0, Failed: 0,
  Skipped: 0.** (Brief said 208/208; the suite now reports 227, all green either way.)
- Note: the test project file-links app sources; it does not link the three files I edited,
  so the fixes don't change test coverage; the Android compile itself is the new coverage.

## Emulator outcome

**Not attempted beyond environment verification, documented as blocked, per the task's
"timebox, don't burn hours" instruction.**

- `/dev/kvm` does not exist; `/proc/cpuinfo` shows no `vmx`/`svm` flags → zero hardware
  virtualization. An x86_64 AVD would run under QEMU TCG (pure software emulation).
- Booting a modern (API 36) system image under TCG is not viable in reasonable time
  (tens of minutes to boot, if at all; downloads alone would be ~1.5 GB through the
  sandbox proxy), so no AVD was created and no system image was downloaded.
- Precise blocker: **no KVM on this host**. Unblocked by: a KVM-enabled Linux host,
  a physical Android device (`adb install` the signed APK above), or CI with hardware
  acceleration (e.g. GitHub Actions + `reactivecircus/android-emulator-runner`).
- Consequently the **Android PDF handoff path was not observed on-device**; it remains
  code-reviewed only (manifest `<queries>` + `Launcher.OpenAsync(OpenFileRequest)` via
  Essentials' sharing root; compiles, unverified at runtime).

## iOS

Not attempted at all: no Mac, hard block, as instructed. (The iOS TFM can't even restore
on Linux: `NETSDK1178`: `Microsoft.iOS.Sdk.net10.0_26.5` workload packs don't exist for
this platform. That's why the build pins `-p:TargetFrameworks=net10.0-android`.)

## Environment gotchas worth recording

1. **`/tmp` is a 512 MB tmpfs and fills fast.** The maui-android workload staging
   (~500 MB+) filled it to 98% and would have failed the install; re-ran with
   `TMPDIR=/home/hatch/.tmpdir` (overlay fs, 7.3 GB free). Moved the Android SDK +
   JDK into `~/workspace/opencoursewareoffline/android-tools/` for the same reason.
2. **Java through the sandbox egress proxy is broken for sdkmanager.** `sdkmanager`
   throws `NoSuchElementException` in `doTunneling` and later "IO exception while
   downloading manifest" even with explicit `-Dhttps.proxy*` system properties, while
   `curl` through the same proxy works fine. Workaround used: downloaded the three
   package zips via `curl` (URLs parsed from `repository2-1.xml`) and laid out the SDK
   directories manually, writing the standard `android-sdk-license` hash file by hand
   (`sdkmanager --licenses` never ran successfully).
3. **`export A=x B=$A` does not chain**: `$A` expands to the *old* value, silently
   producing empty derived vars (this bit JAVA_HOME/PATH twice). Use separate `export`
   statements.
4. **The Android build needs a JDK, not a JRE**: XA5300 "Could not find required file
   `jar`" until Temurin JDK 17 replaced the JRE.
5. **xunit v2 console runner is net6.0-only** and needs `DOTNET_ROLL_FORWARD=LatestMajor`
   to run on the net10 runtime in this sandbox.

## Standards followed / deviations

- Read `~/workspace/standards/` (v0.3) and the `dotnet-standards` skill at kickoff.
- Fixes are minimal/conventional; package version explicitly pinned (no `NoWarn`
  suppression) per conventions #12; nullable/file-scoped conventions untouched.
- **No deviations** from the standards; nothing added to `notes/decisions.md`
  (left for the coordinator to decide whether the manual SDK-license hash or the
  `TargetFrameworks` command-line pin warrant entries).
- Possible harvest candidate for `~/workspace/standards/`: none new beyond the
  gotchas above, which are environment-specific rather than cross-project patterns.
  (The "TMPDIR on roomy fs" and "sdkmanager-vs-proxy → curl fallback" notes may
  belong in the project's `notes/env-setup.md`; coordinator's call.)
