# OCW Offline — Developer Handoff v43

**Status:** App still never compiled/run — see `PROCESS_v43.md` Rule 1, unchanged, permanent. **Read `PROCESS_v43.md` in full before this file. Do not read `AUDIT_TRAIL_v43.md`'s history by default — see PROCESS Rule 6.**

---

## 0. Start-of-round check

1. Fresh conversation — no earlier version of this app exists in this chat, so there's nothing in-chat to diff v42's zip against. A fine, honest "nothing to compare" outcome per PROCESS §0.1, not a gap.
2. `PROCESS_v42.md` confirmed unchanged from what was handed in; copied forward verbatim as `PROCESS_v43.md` — no rule changes this round, no new Process Changelog entry.
3. **This round is the designated Rule 8 scan round** (cadence locked v38: every 5th round starting at v38 — v38, v43, v48…). Per Rule 8: scan-only, diff-review the tree, re-rank the backlog, produce nothing but an updated table. **Exception, logged not silently absorbed:** the person, mid-round, pasted an external review and directly instructed a docs-only addition anyway (`docs/FIRST_BUILD_CHECKLIST.md`, see §1) — a deliberate one-off override of Rule 8's "nothing but the table" line, at the person's explicit instruction, not a standing rule change. See `AUDIT_TRAIL_v43.md`'s v43 entry for the full exchange.
4. Baseline `devtools/csharp_lint.py` run before touching anything: exit 0, no advisories, same 2 pre-existing empty-catch entries as every prior clean round.

**Diff against v42:** no in-chat prior version to diff, so the tree was reviewed directly instead (§0.1). No files touched — `OcwOffline_v43.zip`'s app tree is byte-identical to v42's. `PROCESS_v41.md` → `PROCESS_v43.md`: unchanged, copied verbatim. `AUDIT_TRAIL_v42.md` → `AUDIT_TRAIL_v43.md`: v1–v42 preserved, one v43 entry appended.

---

## 1. What changed this round

Nothing in **app code** — no `.cs`/`.xaml` touched, that discipline held. One docs-only addition, as a deliberate logged Rule 8 exception (see §0.3, `AUDIT_TRAIL_v43.md`): **`docs/FIRST_BUILD_CHECKLIST.md`**, sourced from an external Gemini review the person pasted mid-round. Four platform-runtime risks named — iOS `WKWebView` local-file access in `ArtifactViewerPage.xaml.cs`, `MediaElement` seek-on-open timing in `VideoPlayerPage.xaml.cs`, Android PDF `FileProvider` hand-off in the same file, `CollectionView` progress-update thrashing across `DownloadManager.cs`/`CourseViewModel.cs` — each independently verified against this tree before inclusion, not taken on the review's word. Per Rule 12 (bridge-document-sourced additions get a backlog row even when non-code), logged as a resolved row below rather than folded in silently.

The scan itself: reviewed every open backlog row against the current tree, and confirmed two things that were living in loose context but not tracked as open rows are in fact already resolved, so there's nothing to add for them:

- `IDownloadManager`/`FakeDownloadManager` seaming is already in the tree (interface extracted v34) with `CourseViewModelDownloadCommandTests.cs` and `DownloadsDashboardViewModelCommandTests.cs` already exercising it against the fake.
- Both Rule 9 fixtures (`docs/contracts/mit-learn-courses-v1.json`, `docs/contracts/communitytoolkit-maui-mediaelement-version.md`) are current per their own provenance notes (last reconfirmed v41 and v36 respectively) — no drift, no re-check needed.

No row's impact rating changed. See §2.

---

## 2. What's left — impact-rated backlog, append-only (see `PROCESS_v43.md` Rule 5)

Reviewed in full this round (Rule 8); order confirmed, not changed. Nothing resolved, nothing added.

| Item | Impact | Notes |
|---|---|---|
| Confirm `api.learn.mit.edu` (production) matches the RC host verified at v14 | Medium | **Categorically blocked. Do not check this again, in any way, for any reason — see PROCESS Rule 10.** Only reopens if the person pastes a live fetch result into chat, or a real build/compile session happens. No round decides on its own that its tool is an exception. |
| Confirm a real server-side search/text param on the MIT Learn courses API | Low-Medium | Not categorically blocked — a live RC-endpoint test with a search param would resolve this, and hand-modifying a known-good URL's query string is confirmed blocked (v41). The actual backend is `mitodl/mit-learn` (a real Django app using `drf-spectacular` to generate its OpenAPI spec from the views) — not `ocw_oer_export`, which is only a client-side mirror. Reading that repo's course-view/filter code would settle this without touching any MIT Learn host, but no search so far has surfaced a direct link to the specific file. **v43 bonus-attempt note:** `mitodl/course-search-utils` is real and confirms MIT Open has genuine search-param support — but it's a different, OpenSearch-backed host/service (`mitopen.odl.mit.edu`, discussions/search) from the `api.../api/v1/courses/` list endpoint this app actually calls. Don't mistake that repo's existence for an answer to this specific question. A future round should still search for `mitodl/mit-learn`'s own course views/serializers file specifically. |
| Test project's xunit v2 trio vs. current xunit.v3 + Microsoft Testing Platform | Low-Medium | Investigated v39 (package/attribute requirements researched, migration itself deferred as too risky without a compiler in the loop — see AUDIT_TRAIL v39). |
| `VideoPlayerPage.xaml.cs`/`ArtifactViewerPage.xaml.cs` — remaining coverage | Low — **blocked by Rule 1** | The pure-logic layer (`VideoPlaybackLogic`/`ArtifactViewingLogic`) is fully covered already; what remains is code-behind that only runs against real MAUI controls post-`InitializeComponent()`. Same "no compiler/runtime" ceiling as the rest of the project. Only reopens with an actual build/compile session. |
| Direct live read of current `dotnet/maui` `Launcher.android.cs` (Android) | Low | Still open. Two *other* `dotnet/maui` source files have surfaced as direct, fetchable `blob/`-URL results in past rounds, so the categorical block is specifically about `tree/`-style folder listings and un-surfaced text mentions, not GitHub blob URLs as a category. **v43 bonus-attempt note:** the archived `xamarin/Essentials` repo's own `Launcher.android.cs` (pre-MAUI predecessor) surfaced as a fetchable `blob/` URL this round — wrong repo for what this item asks about, not fetched, not a resolution. A future round with the *current* `dotnet/maui` file's own `blob/` URL surfaced by a search result could actually fetch it; don't re-attempt by guessing/constructing the URL. |
| `docs/contracts/mit-learn-courses-v1.json` — the search/text-param question above | — | Folded into the search-param row; not a separate item. |

*(Full table with all resolved-item history lives in `HANDOFF_v39.md` and earlier; nothing below Low was dropped, just not re-typed here.)*

**Resolved this round, Rule 12 row (bridge-document-sourced, not code):** `docs/FIRST_BUILD_CHECKLIST.md` added — four platform-runtime risks (iOS `WKWebView` local files, `MediaElement` seek race, Android PDF `FileProvider`, `CollectionView` progress thrash) sourced from an external Gemini review, each independently verified against this tree. Nothing to action now — it's a checklist for whenever an actual compiled build happens, not a task for this loop. See §1.

---

## 3. ⚠️ Still a blind spot

Unchanged from v42: the test project has never compiled; the production-host question for MIT Learn remains open and permanently closed to further checking barring the person's own action; the `VideoPlaybackLogic`/`ArtifactViewingLogic` extraction is reasoned through but unverified against a real MAUI runtime, and that gap is structural, not a to-do item, for the two view pages built on top of it.

**Still unresolved from v40:** nothing currently enforces that a future structural change gets reflected back into `docs/ARCHITECTURE.md` (possible future Rule 14, not adopted).

`MauiProgram.cs` unchanged this round.

---

## 4. Autonomy

Use your judgment — this isn't a spec. Hard constraint from v1, still standing: no YouTube extraction, no multi-user redistribution features. Since v28: every new/changed method gets a unit test in the same round (Rule 11); test bodies never carry Arrange/Act/Assert comments (Rule 11, hard). Since v38: bridge-document-sourced fixes get an explicit backlog row (Rule 12); every 5th round (this one, v43; next: v48) is a designated scan-only round (Rule 8); bonus-token attempts are additive only, unfinished rounds amend in place (Rule 13). Since v40/v41: Rule 10 distinguishes a categorically-blocked item from a genuinely hard-to-find fact, and `api.learn.mit.edu` specifically is closed to any further checking by any method, for any reason, ever. See PROCESS's own Process Changelog for full reasoning; no new rule changes this round.

---

## 5. Before you hand this off again

Full checklist is in `PROCESS_v43.md`. Specifically for this file: the next round is a normal (non-scan) round — pick the top of §2's backlog and build, subject to Rule 10's categorical block on the first row. Append to `AUDIT_TRAIL_v43.md` — don't touch the entries above it, and don't read them first (Rule 6). Per Rule 13: this file only becomes v43's *final* state once the person downloads it — any further "one more thing" before that point amends this same file again, it doesn't start `HANDOFF_v44.md`.
