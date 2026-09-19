# Questions

Open questions for the user.

## MIT Learn API research (2026-09-18, stream 3)

Two open items carried from `archive/v43-process-rules/HANDOFF_v43.md` (rows 37–38). Researched against the real backend repo (`mitodl/mit-learn`, Django + DRF + drf-spectacular).

### Q1. Confirm `api.learn.mit.edu` (production) matches the RC host verified at v14

**Status: RESOLVED by live verification, 2026-09-18.** The v43 PROCESS Rule 10 block was lifted by user directive: those rules governed the free-Claude round robin and do not apply to Muse sessions (rule docs archived at `archive/v43-process-rules/` with a supersession README).

Direct fetch of `GET https://api.learn.mit.edu/api/v1/courses/?platform=ocw&limit=1` returns HTTP 200 with the same DRF envelope as the RC host: `{count, next, previous, results[]}` and the same serializer fields (id, topics, offered_by, platform, course_feature, departments, certification, runs[] with instructors/image/level/delivery/format/pace, image, free, resource_type_group, best_run_id, learn_url, url_slug, resource_type, course.course_numbers, readable_id, title, description, url).

Same-day comparison fetch of `https://api.rc.learn.mit.edu/api/v1/courses/?platform=ocw&limit=1`: identical shape, count 2903 vs production count 2586. The count delta is expected (staging snapshot vs production published set) and irrelevant to the app, which paginates via `next` and parses the shared schema.

**Consequence for the app:** the production BaseUrl already used by `OcwCatalogService` is confirmed live and schema-compatible. No code change needed.

### Q2. Confirm a real server-side search/text param on the MIT Learn courses API

**Answer: NO, no such param exists. Resolved from source.**

The endpoint the app calls (`GET /api/v1/courses/`) is served by `CourseViewSet`, which subclasses `BaseLearningResourceViewSet`:

```python
class BaseLearningResourceViewSet(viewsets.ReadOnlyModelViewSet):
    permission_classes = (AnonymousAccessReadonlyPermission,)
    filter_backends = [MultipleOptionsFilterBackend]   # <- NOT DRF SearchFilter
    filterset_class = LearningResourceFilter
    lookup_field = "id"

class CourseViewSet(BaseLearningResourceViewSet):
    """Viewset for Courses"""
    serializer_class = CourseResourceSerializer
    def get_queryset(self):
        return self._get_base_queryset(resource_type=LearningResourceType.course.name).filter(published=True)
```

The full filter list on `LearningResourceFilter` is: `free`, `department`, `resource_type`, `offered_by`, `platform`, `level`, `topic`, `course_feature`, `readable_id`, `resource_id`, `sortby`, `delivery`, `certification_type`, `resource_type_group` (+ `professional`, `certification` via Meta). There is no `search`/`q`/`text`/`query` param anywhere in the view or filterset, and no DRF `SearchFilter` in `filter_backends`.

**Consequence for the app:** `CatalogViewModel.FilterEntries` (client-side title filtering over already-loaded entries) remains the correct approach; there is nothing server-side to switch it to. Real text search on MIT Learn goes through a different, OpenSearch-backed service (`learning_resources_search` / `lr_search`, used by the learn.mit.edu frontend), not the DB-backed `/api/v1/courses/` list endpoint.

**Corroborating API facts (same sources):**
- Pagination is DRF limit/offset (`DefaultPagination`: default 10, max 100), response shape `{count, next, previous, results}`: matches `OcwCatalogService`'s `limit`/`offset` params and `next`-based `HasMore`.
- No auth required for anonymous GET (`AnonymousAccessReadonlyPermission`; issue #3710: "against production (no auth required)").
- `platform=ocw` is a valid filter on the courses endpoint (issue #189's own example: `/api/v1/courses/?platform=ocw&offset=80&limit=20`); `offered_by=ocw` is also valid (used by a third-party OCW ingest skill). The app's `platform=ocw` is correct.
- No published rate-limit docs found for the anonymous API; mit-learn issue #3710 ("sortby=-views runs a full-table view-event aggregate") actually recommends MIT *add* rate limiting for unauthenticated clients; so assume none is enforced today.

**Sources:**
- https://github.com/mitodl/mit-learn/blob/daed199e5d5a3ef28c8bfcc1f3c0bd70a692bcda/learning_resources/views.py: viewsets, filter backends (lines ~183–184, ~387–398)
- https://github.com/mitodl/mit-learn/blob/daed199e5d5a3ef28c8bfcc1f3c0bd70a692bcda/learning_resources/filters.py: complete `LearningResourceFilter` param list (lines ~40–178)
- https://github.com/mitodl/mit-learn/issues/3710: pagination defaults (`DefaultPagination`/`LargePagination` in `main/pagination.py`), anonymous access, `platform`/`resource_type`/`sortby`/`limit`/`offset` query params
- https://github.com/mitodl/mit-learn/issues/189: `/api/v1/courses/` DRF response shape; plan to move lists to OpenSearch-backed `LearningResourcesSearchView`
- https://github.com/fuzheado/mit-ocw-wiki/blob/HEAD/.claude/skills/ocw-ingest/SKILL.md: independent consumer using `/api/v1/courses/?offered_by=ocw&limit=100&offset=N`, no auth, fully public

**Residual uncertainty:** the top-level `courses` route registration itself (which urls.py registers `CourseViewSet` at `^api/v1/courses/`) wasn't directly read (`learning_resources/urls.py` only shows the nested `courses/<id>/contentfiles` route), but the endpoint's existence and behavior are confirmed three independent ways (v14 RC verification, issue #189's example, the third-party ingest skill), and `CourseViewSet` inherits the same filter backends/filterset regardless of where it's mounted, so the search-param answer is unaffected.

**Code impact:** none. `OcwCatalogService`'s endpoint (`/api/v1/courses/?platform=ocw&limit=&offset=`), `next`-based paging, and field usage match the official backend source. No changes made.
