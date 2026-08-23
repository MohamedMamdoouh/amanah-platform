# Sub-phase 06 — Admin Moderation Search

**Status:** Not started  
**Prerequisites:** [Sub-phase 03 — Moderation Queue](./SUBPHASE-03-03-moderation-queue.md), Phase 02 search column

---

## 1. Summary

Implement `GET /api/admin/moderation/search` — keyword search over reports including `Pending Review` and `Rejected` statuses. Reuses `normalizedSearchText` and `ArabicNormalizer` from Phase 02. Same all-terms AND matching as public browse (Phase 04) but scoped to non-public statuses for admin duplicate detection.

---

## 2. SPEC references

| SPEC section | Topic |
| ------------ | ----- |
| Section 4.4 | Admin moderation search scope |
| Section 16 | Search column and normalization |
| Section 5.5 | Duplicate detection before approval |

---

## 3. What you will learn

- Reusing `ArabicNormalizer.BuildSearchTerms` for query parsing
- `ILIKE` with AND-ed terms against `normalizedSearchText`
- Scoping status filter: `Pending Review`, `Rejected` (and optionally `Published` for admin — include all non-terminal for moderation)
- Pagination matching queue pattern

**Files to read after implementing:**

- `api/Services/Moderation/ModerationSearchService.cs`
- `api/Controllers/Admin/ModerationController.cs` (search action)
- `api.Tests/Services/ModerationSearchServiceTests.cs`

---

## 4. Deliverables

### Endpoint

| Method | Route | Auth | Purpose |
| ------ | ----- | ---- | ------- |
| GET | `/api/admin/moderation/search` | Admin | Keyword search |

### Query parameters

| Param | Required | Rules |
| ----- | -------- | ----- |
| `q` | Yes | Min 2 chars after normalize; max 100 |
| `status` | No | Filter: `pending-review`, `rejected`, `published`, or omit for all admin-visible |
| `page` | No | Default 1 |
| `pageSize` | No | Default 20, max 50 |

### Search logic

1. Normalize query via `ArabicNormalizer.BuildSearchTerms(q)`
2. Reject empty term list → `400`
3. Base query: reports where status in admin scope
4. For each term: `normalizedSearchText ILIKE '%' || term || '%'`
5. AND all terms together
6. Sort: `createdAt` descending (most recent first — unlike FIFO queue)
7. Return `ModerationQueueItemDto` list (shared shape with search — see sub-phase 06): `id`, `type`, `title`, `status`, `categoryName`, `reporterDisplayName`, `createdAt`, `thumbnailUrl`

### Performance note

`pg_trgm` GIN index is created in Phase 04 [sub-phase 01](../phase-04/SUBPHASE-04-01-search-index.md). **Do not** add a duplicate trigram migration here — if Phase 04 is not yet deployed, moderation search still works via sequential scan at low volume; index lands before public browse ships.

---

## 5. Step-by-step implementation order

1. Implement `ModerationSearchService.SearchAsync`
2. Add controller action with admin guard
3. Unit tests: Arabic normalization matches (أحمد finds احمد)
4. Integration tests: multi-term AND logic
5. Test status filter combinations
6. Verify search finds `Pending Review` reports not visible in public API

---

## 6. Out of scope

- Public browse search UI (Phase 04)
- Full-text `tsvector` (post-v1)
- Search across claim text or chat
- Angular search UI (sub-phase 09)

---

## 7. Validation gate

### Automated tests

- [ ] Search "هاتف سامسونج" returns report containing both terms (any order)
- [ ] Alef variant query matches stored text
- [ ] Query with one non-matching term returns empty
- [ ] `status=pending-review` excludes rejected
- [ ] Non-admin → `403`
- [ ] Query < 2 chars → `400`
- [ ] Pagination works

### Manual smoke checklist

- [ ] Create two similar pending reports; search finds both
- [ ] Search term from title only — still matches

---

## 8. Exit criteria

- [ ] Search endpoint passes Arabic normalization tests
- [ ] Ready for admin UI search box in sub-phase 09
- [ ] Mark sub-phase 06 complete in [phase-03/README.md](./README.md)
