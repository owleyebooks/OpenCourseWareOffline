# Environment setup

Shared toolchain and VM rebuild notes: `~/workspace/muse-ops/environment.md`.
This file holds only project-specific deltas (DB names, ports, test env vars).

## .NET SDK location

No system `dotnet` on PATH. Use the SDK installed for the RanksAndBeans
project:
`DOTNET_ROOT=/home/hatch/workspace/ranksandbeans/.dotnet`
(`dotnet` binary in the same dir). Version: 10.0.401.

## Test runner quirk (2026-09-18)

`dotnet test` (vstest) does not work in this sandbox: the testhost process
starts but never connects back: `vstest.console` aborts after 90s
("failed to connect to testhost process"). Observed symptom: testhost is
launched with `--endpoint 127.0.0.1:040573` (note the zero-padded port) while
vstest listens on `127.0.0.1:40573`. Raising `VSTEST_CONNECTION_TIMEOUT` to
300s did not help.

Workaround: run the xunit v2 console runner directly, bypassing vstest:

```
export DOTNET_ROLL_FORWARD=LatestMajor
cd work/OcwOffline.Tests/bin/Debug/net10.0
dotnet exec /tmp/xunit-console/tools/net6.0/xunit.console.dll OcwOffline.Tests.dll
```

(`xunit.runner.console` 2.9.2 nupkg fetched from nuget.org 2026-09-18 into
`/tmp` is ephemeral; re-fetch if the VM was replaced.)
The console runner is net6.0-only: set `DOTNET_ROLL_FORWARD=LatestMajor` so it
runs on the installed net10 runtime.

## Android toolchain (2026-09-18)

- `dotnet workload install maui-android` → maui-android 10.0.20/10.0.100
  (SDK 10.0.400). Plain `maui` workload is NOT supported on Linux.
- Android SDK hand-laid at `~/workspace/opencoursewareoffline/android-tools/`
  (NOT synced to Drive; excluded in `notes/drive_sync.json`): cmdline-tools
  13114758, platform-tools r37.0.1, `platforms;android-36`, build-tools 36.0.0,
  Temurin JDK 17.0.20.1 at `android-tools/jdk17/jdk-17.0.20.1+1` (a JRE is
  insufficient (the build needs `jar`, XA5300 otherwise).
- Build for Android only with the TFM pin (plain `-f net10.0-android` still
  resolves the iOS workload → NETSDK1178 on Linux):
  `dotnet build OcwOffline/OcwOffline.csproj -p:TargetFrameworks=net10.0-android`
  with `ANDROID_HOME`/`ANDROID_SDK_ROOT` and `JAVA_HOME` exported.
- Emulator blocked: no `/dev/kvm`, no `vmx`/`svm` CPU flags. iOS blocked:
  no Mac (can't even restore on Linux: NETSDK1178).

## Sandbox gotchas (2026-09-18)

- `/tmp` is a 512 MB tmpfs; workload staging filled it to 98%. Re-run heavy
  installs with `TMPDIR=/home/hatch/.tmpdir` (roomy overlay fs).
- Java through the sandbox egress proxy is broken for `sdkmanager`
  (`NoSuchElementException` in `doTunneling`); `curl` through the same proxy
  works. SDK zips were fetched via curl (URLs from `repository2-1.xml`) and
  laid out manually, with the `android-sdk-license` hash file written by hand.
- `export A=x B=$A` does not chain expansions, so use separate `export` lines.
