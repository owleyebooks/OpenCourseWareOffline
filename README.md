# OCW Offline

An offline-first MIT OpenCourseWare course downloader and tracker for
Android and iOS, built with .NET MAUI.

Browse the OCW catalog, download course materials (PDFs, lecture videos)
for offline use, track download progress, and keep watching where you left
off. All course data lives in a local SQLite database, so the app works
fully offline once content is downloaded.

## Content attribution

Course content is provided by **MIT OpenCourseWare** (https://ocw.mit.edu)
and is licensed **Creative Commons Attribution-NonCommercial-ShareAlike
(CC BY-NC-SA)**. The CC BY-NC-SA license applies to the *course content*
the app downloads, not to this repository's code.

**This app is not affiliated with or endorsed by MIT.**

## License

The code in this repository is **MIT licensed** (see `LICENSE`). The
copyright holder is recorded there; correct it if it names the wrong
entity (see `notes/decisions.md`).

## Build prerequisites

- .NET 10 SDK (10.0.401 verified)
- `maui-android` workload: `dotnet workload install maui-android`
  (the plain `maui` workload is not supported on Linux)
- Android SDK (API 36, build-tools 36.0.0) and JDK 17, with
  `ANDROID_HOME`/`ANDROID_SDK_ROOT` and `JAVA_HOME` exported
- iOS builds require a Mac (`net10.0-ios` cannot restore on Linux)

Build Android only with the TFM pin (plain `-f net10.0-android` still
resolves the iOS workload on Linux):

```
dotnet build work/OcwOffline/OcwOffline.csproj -p:TargetFrameworks=net10.0-android
```

See `notes/env-setup.md` for the full toolchain layout used in this
environment.

## Tests

`dotnet test` (vstest) does not work in this sandbox: the testhost never
connects back. Run the xUnit v2 console runner directly instead:

```
export DOTNET_ROLL_FORWARD=LatestMajor
cd work/OcwOffline.Tests/bin/Debug/net10.0
dotnet exec /tmp/xunit-console/tools/net6.0/xunit.console.dll OcwOffline.Tests.dll
```

(`xunit.runner.console` 2.9.2, fetched from nuget.org; the console runner
is net6.0-only, hence the roll-forward flag.) Full details in
`notes/env-setup.md`. On a healthy machine or CI, plain `dotnet test`
should work.

## Repository links (TODO)

- GitHub repository: **TODO** (fill in when the repo is created; also
  update `OcwOffline/ExternalLinks.cs`)
- Hosted privacy policy: **TODO** (fill in when hosted; also update
  `OcwOffline/ExternalLinks.cs` and `docs/privacy-policy.md`)

## Docs

- `docs/store-listing.md`: draft Play/App Store listing copy
- `docs/privacy-policy.md`: draft privacy policy
- `docs/release-checklist.md`: step-by-step release runbook (signing,
  versioning, AAB, store submission)
- `notes/device-test-plan.md`: device verification plan for the known
  risks (WKWebView local files, video seek race, PDF handoff)
- `notes/decisions.md`: dated record of every deliberate decision
- `CONTRIBUTING.md`: how to contribute
