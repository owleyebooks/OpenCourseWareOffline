# Stream report: MIT Learn API research (stream 3 of 5)

Date: 2026-09-18. Task: extract the two open MIT Learn API questions from specs, research the real API, update `notes/questions.md`, fix code if it contradicts the docs.

## The two questions (verbatim from `archive/v43-process-rules/HANDOFF_v43.md`, rows 37–38)

1. **"Confirm `api.learn.mit.edu` (production) matches the RC host verified at v14"** (Medium; categorically blocked per PROCESS Rule 10 (v40/v41): do not check this again in any way, for any reason).
2. **"Confirm a real server-side search/text param on the MIT Learn courses API"** (Low-Medium, not blocked). Backend is `mitodl/mit-learn` (Django + DRF + drf-spectacular); the handoff suggested reading that repo's course-view/filter code.

## What the docs/source say

**Q1:** Not touched. Rule 10 honored: no request of any kind was made to `api.learn.mit.edu`.

**Q2: RESOLVED, no server-side search/text param exists on `/api/v1/courses/`.** Read the actual backend source at mit-learn commit `daed199e5d5a3ef28c8bfcc1f3c0bd70a692bcda`:

- `learning_resources/views.py`: `BaseLearningResourceViewSet` sets `filter_backends = [MultipleOptionsFilterBackend]` (a custom multi-value django-filter backend, **not** DRF's `SearchFilter`) and `filterset_class = LearningResourceFilter`. `CourseViewSet(BaseLearningResourceViewSet)` filters to `resource_type=course` + `published=True` and serializes with `CourseResourceSerializer`.
- `learning_resources/filters.py`: `LearningResourceFilter` exposes exactly: `free`, `department`, `resource_type`, `offered_by`, `platform`, `level`, `topic`, `course_feature`, `readable_id`, `resource_id`, `sortby`, `delivery`, `certification_type`, `resource_type_group` (plus Meta fields `professional`, `certification`). No `search`/`q`/`text`/`query` param exists anywhere in the view or filterset.
- Genuine text search on MIT Learn lives in a different, OpenSearch-backed service (`learning_resources_search` / `lr_search`, what the learn.mit.edu frontend uses), not the DB-backed `/api/v1/courses/` list. This matches the v43 note warning not to confuse `mitodl/course-search-utils` (a different host/service) with this endpoint.

Corroborating facts gathered along the way:
- **Pagination:** DRF limit/offset (`DefaultPagination`: default_limit 10, max_limit 100; `LargePagination`: 1000/1000). Response `{count, next, previous, results}` (mit-learn issues #189, #3710).
- **Auth:** anonymous GET needs no credentials (`AnonymousAccessReadonlyPermission`; #3710: "against production (no auth required)").
- **Params:** `platform=ocw` is valid on `/api/v1/courses/` (#189's own example); `offered_by=ocw` also valid (third-party OCW ingest skill). No published rate-limit docs found; #3710 actually recommends MIT *add* rate limiting for anonymous clients, so none should be assumed.

Source URLs (all in `notes/questions.md` with line refs): the two blob files above, mit-learn issues #189 and #3710, and the fuzheado/mit-ocw-wiki ingest skill.

## Changes made

- **`notes/questions.md`**: rewritten: both questions, the verdict on each, full source list with URLs, residual uncertainty, and the code-impact assessment (see file).
- **Code: no changes.** `work/OcwOffline/Services/OcwCatalogService.cs` matches the official backend: correct endpoint (`/api/v1/courses/`), valid `platform=ocw` filter, valid `limit`/`offset` params (within max 100), `next`-field-based `HasMore`. Nothing contradicts the docs, so nothing was touched.

## Consequence for the app

`CatalogViewModel.FilterEntries` (client-side title filtering over loaded entries) remains the correct design; there is no server-side search param to switch to. If server-side search is ever wanted, it would mean calling the separate OpenSearch-backed search service, which is out of scope for this stream.

## Test results

No code was modified, so the test suite was not re-run (state unchanged). All 208 tests were last green per the coordinator's baseline; the "run tests" requirement was conditional on touching code.

## Remaining uncertainty

- Minor: the top-level route registration mounting `CourseViewSet` at `^api/v1/courses/` wasn't directly read (`learning_resources/urls.py` shows only the nested `courses/<id>/contentfiles` route; the main urls file fetch failed). Doesn't affect the verdict: the endpoint demonstrably exists (v14 RC verification, #189 example, third-party consumer), and the viewset inherits the same filter backends regardless of where it's mounted.
- Q1 (production-vs-RC host match) remains open and blocked; only the user pasting a live fetch or a real build session reopens it.
