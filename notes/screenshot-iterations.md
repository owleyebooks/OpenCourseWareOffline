# Screenshot pipeline iterations

Goal: get real screenshots of the OCW app's CoursePage rendering on an emulator.
Mike's standing orders (2026-09-23): closed loop until figured out; track iteration
count; if looping/chasing tail, stop and get fresh eyes (another agent, web search,
Stack Overflow). No endless spin.

## Loop rules
- Max 5 more iterations from #11 before mandatory fresh-eyes review.
- Same failure twice in a row = chasing tail: stop, get fresh eyes immediately.
- Every check reports to Mike no matter what (20 min first check, every 10 after).

## History
1. Run 35776445987 (API 29): white screen, empty content tree, CreateMauiApp never ran.
2. Run 35796806580 (API 29): AndroidEnableMarshalMethods=false. Identical hang. Ruled out.
3. Run 35808568985 (API 29): added [Application] to MainApplication. CreateMauiApp ran,
   then XamlParseException: InvertedBoolConverter not found.
4. Run 35811964682 (API 29): page-local converter instances. Full startup, toolbar
   rendered, but content region blank white. Entry filled viewport, siblings missing.
5. Run 35883669887: logical-tree diagnostic failed to compile (IView has no IsVisible).
6. Run 35888598738 (API 29): stock MAUI hello-world control. Label rendered full-viewport,
   button invisible with corrupt/offscreen bounds. Proved: not OCW architecture, it's the
   .NET 10 MAUI runtime + API 29 emulator combo.
7. Run 35902326313 (API 34): hello-world laid out correctly. API 29 cleared as the culprit.
8. Run 35908955698 (API 34): OCW run failed, workflow still launched com.companyname.helloworld
   (my leftover). Monkey aborted, no artifacts.
9. Run 35917515886 (API 34): OCW launched, toolbar rendered (OCW/Browse/Downloads/About),
   no crash, no XamlParseException. But emulator System UI ANR dialog covered content.
10. Run 35923050328: ANR-dismiss push rejected in 0s, invalid YAML from multi-line python3 -c
    breaking the script: | block scalar (my error).
11. Run 4c36737 (API 34, in progress): YAML fixed (single-line python, verified against real
    dialog XML, tap at 159,382), ANR dismiss via "Wait" tap. Check at 18:17 EDT.
12. Run 35925543226 (API 34): ANR-dismiss tap landed at the right coords (160,391)
    but the dialog persisted all run. Root cause per web research (multiple CI
    projects): dialogs can appear at ANY moment on a loaded emulator, so one-shot
    dismissal is structurally wrong. Fix: `hide_error_dialogs=1` right after boot
    (the standard suppression), dismiss-before-EVERY screenshot via new
    tools/ci/dismiss_anr.sh + find_wait.py, dropped the mid-run SIGQUIT thread
    dump (unneeded load), moved ui.xml dump after shot3 (repeated dumps can wedge
    a slow emulator). Pushed <pending>, check per 20/10 standard.
13. Layout restructure (the actual fix, no new run yet): shot1 of run 35928080967
    showed only the ANR dialog, but logcat accessibility dump revealed the real
    OCW layout underneath: toolbar + search Entry rendered, every later control
    at corrupt Y (Fetch ~16.7M, status ~33.5M, Resources ~50.3M, RV1 ~67.1M,
    Lecture Videos ~83.9M, RV2 ~100.7M, storage ~117.4M). Entry (child 1) fine,
    children 2..n each offset by ~16.7M (2^24). Same signature as the API 29
    VSL bug, so this is the .NET 10 MAUI Android measure path corrupting
    ScrollView > VerticalStackLayout children, not an app startup failure.
    Web research confirms ScrollView must not wrap CollectionView (MAUI docs)
    and VSL gives children infinite height (kills CollectionView virtualization).
    Fix: CoursePage.xaml restructured to Grid (Auto header / Auto tabs /
    * lists / Auto storage), no ScrollView, one visible CollectionView at a
    time via new ShowingLectures VM property + ShowTabCommand (Resources and
    Lecture Videos tab buttons, active tab disabled). AboutPage.xaml dropped
    its ScrollView too (same broken pattern, content fits without scrolling).
    XAML validated locally (well-formed, all StaticResource keys and bindings
    resolve); full Android compile happens in CI (no Android SDK on this VM).
14. Run 35931688997 (screenshots #26, commit ea75266, layout fix): all three
    workflows green (screenshots, android #34, prose-lint #34). App launched
    clean on the new XAML: logcat shows "OCWDIAG: unexpected content shape:
    Grid" (the temp diagnostic expecting ScrollView+VSL), no crash, no
    XamlParseException. But the capture was INCONCLUSIVE: System UI ANR'd
    during capture, shot1 shows only the ANR dialog, shots 2-3 show a blank
    white content area under an intact toolbar, and the ui.xml dump is stale
    (shows a ScrollView ancestor and is missing Fetch/tab/list controls,
    yet the ScrollView does not exist in the shipped XAML, and the dump was
    taken while System UI was ANR'ing). Cannot call PASS or FAIL from this
    capture. Follow-up timer ocw-screenshot-check-13 died: the 19:05 worker's
    self-reschedule via cron.update threw ("scheduled work reported
    incomplete"), runonce never re-armed, no checks ran 19:05-21:48. Lesson:
    never rely on a worker self-rescheduling a runonce; use an interval job
    that cron.removes itself on terminal state instead. Reran run 35931688997
    at ~21:49 EDT; polling via ocw-screenshot-poll-14 (10m interval,
    self-removing on verdict).
15. Rerun of 35931688997 (polled 22:11 EDT, same databaseId, screenshots #26
    rerun at ~21:49): all workflows green, build SUCCEEDED. Capture
    INCONCLUSIVE again: System UI ANR'd during capture (shot1 literally shows
    the "System UI isn't responding" dialog), and the ui.xml dump is stale
    (shows an android.widget.ScrollView ancestor that does not exist in the
    shipped XAML, only 23 nodes, missing Fetch Course / tab buttons / status
    label, storage TextView with suspicious 80-640 range). Meanwhile logcat
    has the app's own OCWDIAG line: "unexpected content shape: Grid", meaning
    the new Grid content was live with no crash and no XamlParseException.
    Same capture defect twice in a row: the emulator's ui dump is not
    trustworthy when System UI is ANR'ing. Cannot call PASS or FAIL; the
    layout fix remains unverified by CI screenshots. Polling job
    ocw-screenshot-poll-14 removed after this report.
15. Deterministic verification via in-app bounds logging (commit 4a27994):
    two consecutive captures of ea75266 were inconclusive (System UI ANR
    both times), so screenshots are no longer the verification path. Added
    temporary OCWLAYOUT diagnostic: 3s after CoursePage appears, the app
    logs MAUI-side x/y/w/h for each named control (SearchEntry,
    FetchButton, StatusLabel, ResourcesTabButton, LecturesTabButton,
    ArtifactsList, LecturesList, StorageLabel) to logcat, which the
    workflow already captures into diag/logcat-full.txt at the end of the
    run. PASS = all visible controls y in 0..700 dp, no million-scale
    values; FAIL = 16.7M signature persists or visible controls collapse
    to zero. x:Name added to the 8 controls; prose lint clean. Pushed
    4a27994 ~22:25 EDT; polling via ocw-screenshot-poll-15 (10m interval,
    self-removing on verdict). Remove the diagnostic before durable
    integration.
16. Layout-bounds diagnostic verdict (run #27, commit 4a27994, polled 22:37
    EDT): build SUCCEEDED (all workflows green), diagnostic lines present in
    diag/logcat-full.txt. Verdict: FAIL. MAUI-side bounds still carry the
    corrupt 2^24 (16777216) scale after the Grid restructure:
      SearchEntry      x=16 y=16       w=16777183 h=16777215 visible=True
      FetchButton      x=16 y=16777243 w=16777183 h=16777215 visible=True
      StatusLabel      x=16 y=33554470 w=16777183 h=16777215 visible=True
      ResourcesTabBtn  x=16 y=16777243 w=16777215 h=16777212 visible=True
      LecturesTabBtn   x=16777239 y=16777243 w=16777215 h=16777212 visible=True
      ArtifactsList    x=16 y=33554470 w=16777183 h=0       visible=True
      LecturesList     x=0 y=0         w=-1      h=-1       visible=False (hidden by design, fine)
      StorageLabel     x=16 y=-16      w=16777183 h=16777215 visible=True
    The x/y walk (dp sums up the parent chain) mixes clean values (16) with
    2^24-scale values, and every visible control reports Width/Height near
    16777216; Y positions accumulate the bad heights (16M, 33M); ArtifactsList
    is zero-height; StorageLabel sits at y=-16. The Grid restructure did not
    fix the Android layout corruption: the layout engine is still producing
    million-scale Width/Height, so the visible page cannot be laid out
    correctly. Next: find the real source of the 2^24 scale (candidate:
    Android handler pixel/dp mapping feeding MAUI, or a Measure pass
    returning garbage) rather than restructuring XAML further. Polling job
    ocw-screenshot-poll-15 removed after this report.
17. HelloWorld control verdict (run #28, commit c8a9cbb, polled 23:06
    EDT): run completed with CONCLUSION FAILURE, no verdict possible.
    Failing step: "Capture screenshots on emulator". Root error: the APK
    install failed because the emulator's package service was not ready:
      adb: failed to install com.companyname.helloworld-Signed.apk:
      cmd: Can't find service: package
    (the preceding "cmd: Failure calling service package: Broken pipe
    (32)" shows the package manager daemon was half-booted). The app never
    launched, so no HWLAYOUT lines were logged and diag/logcat-full.txt was
    never captured. Cannot conclude whether the 2^24 corruption is
    environmental or OCW-specific. The control experiment is still owed; a
    re-run needs the workflow to wait for the package service (e.g. retry the
    install a few times or poll `adb shell pm path` before installing).
    Screenshots workflow on branch screenshot-run restored to the OCW target
    (matching master) so the next experiment starts from the right base.
    Polling job ocw-helloworld-poll-16 removed after this report.
18. HelloWorld control re-run verdict (run #28 re-run, commit c8a9cbb,
    polled 01:35 EDT): CONCLUSION SUCCESS (re-run after the first attempt
    died to an emulator adb-daemon infra flake). HWLAYOUT lines present in
    diag/logcat-full.txt:
      Label  x=30 y=24       w=16777155 h=16777215 visible=True
      Button x=30 y=16777264 w=16777155 h=44       visible=True
    Verdict: CORRUPT. The stock MAUI HelloWorld app shows the same ~2^24
    bounds corruption on API 34: label height 16777215 (2^24 - 1) and the
    button's y pushed to 16M by the bad label height. The 2^24 bug hits stock
    MAUI too, so it is environmental (emulator or MAUI workload), not OCW's
    XAML. Note: this same stock app laid out correctly at iteration 7, so
    the environment itself appears to have degraded between runs; candidates
    are the CI runner image, the emulator system image, or a MAUI workload
    update. Stop XAML restructuring; the next experiment should compare
    environments (fresh emulator image, different API level, CI runner
    pinning) rather than restructuring layout. Screenshots workflow on branch
    screenshot-run already restored to the OCW target (matching master,
    commit c33103e on the remote; the /tmp backup was gone, restored via
    git checkout master --). Polling job ocw-helloworld-poll-17 removed after
    this report.

19. HelloWorld time-series verdict (run #34, databaseId 36040554147, branch
    screenshot-run head 520498d, polled 15:02 EDT): CONCLUSION SUCCESS after
    the adb-via-$ANDROID_HOME fix (runs #31-33 failed on adb daemon flake,
    bare-adb PATH, and a YAML quoting bug). hwlayout2.txt artifact (android-
    screenshots/hwlayout2.txt) contains 1 display line + 22 sample pairs
    (t=2s..44s; logging stopped at 44s instead of 60s, reason unknown):
      Display: w=320 h=640 density=1 orientation=Portrait rate=60.000004
      First (t=2s):  Label x=30 y=24 w=16777155 h=16777215 visible=True
                     Button x=30 y=16777264 w=16777155 h=44 visible=True
      Last (t=44s):  Label x=30 y=24 w=16777155 h=16777215 visible=True
                     Button x=30 y=16777264 w=16777155 h=44 visible=True
    All 22 samples corrupt (16.7M scale), zero sane samples at any t. Density
    reads 1.0, matching the EnsureMetrics-fallback prediction on corrupt
    runs. Verdict: PERMANENT (within this observation window). The
    self-sustaining measure loop held for at least 44s with no convergence,
    and once seeded the corruption did not clear. This strengthens the
    hypothesis that the startup race seeds a non-recovering state rather
    than a transient glitch: the corrupt constraint is fed back on every
    measure pass, so there is no decay path. Next experiment should test
    whether forcing a relayout (orientation change or explicit
    InvalidateMeasure) mid-corruption breaks the loop, or compare
    environments per the iteration-18 note (runner image / system image /
    workload pinning). Polling job ocw-timeseries-poll-19e removed after
    this report.
