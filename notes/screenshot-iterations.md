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
