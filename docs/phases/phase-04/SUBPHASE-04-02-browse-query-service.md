# Sub-phase 02 — Browse Query Service

**Status:** Not started  
**Prerequisites:** [Sub-phase 01 — Search Index](./SUBPHASE-04-01-search-index.md), Phase 01 [ArabicNormalizer](../phase-01/SUBPHASE-01-05-utilities.md)

---

## 1. Summary

Implement `IPublicBrowseQueryService` — the core browse/search query builder with filters, all-terms AND matching, sort, and pagination. Service layer only (returns `IQueryable` or paged entity results) — no HTTP endpoint yet.

---

## 2. SPEC references

| SPEC section | Topic |
| ------------ | ----- |
| Section 4.4 | Browse scope, search, filters, sort, pagination |
| Section 16 | Normalization rules and `ILIKE` matching |

---

## 3. What you will learn

- Scoping to `Published` and `Claim In Progress` only
- `ArabicNormalizer.BuildSearchTerms(q)` for query parsing
- Chaining `ILIKE` per term with AND logic on `normalizedSearchText`
- Combining keyword search with structural filters (AND)
- Sorting by `publishedAt` descending (newest first)
- Date range filter on `dateLostOrFound` (within last 12 months max per SPEC)

**Files to read after implementing:**

- `api/Services/Browse/PublicBrowseQueryService.cs`
- `api/Services/Browse/BrowseFilters.cs`
- `api/Services/Browse/PagedResult.cs`
- `api.Tests/Services/PublicBrowseQueryServiceTests.cs`

---

## 4. Deliverables

### `BrowseFilters` record

| Field | Type | Rules |
| ----- | ---- | ----- |
| `Query` | string? | Optional keyword |
| `CategoryId` | Guid? | Filter by category |
| `GovernorateId` | Guid? | Filter by governorate |
| `Type` | `Lost` / `Found`? | Report type |
| `DateFrom` | DateOnly? | Inclusive; clamped to `[today − 12 months, today]` server-side |
| `DateTo` | DateOnly? | Inclusive; clamped to `[today − 12 months, today]` server-side |
| `Page` | int | Default 1 |
| `PageSize` | int | Default 20, max 20 |

### `IPublicBrowseQueryService`

```csharp
Task<PagedResult<Report>> SearchAsync(BrowseFilters filters, CancellationToken ct);
```

### Query rules

**Base filter:**

```sql
status IN ('Published', 'Claim In Progress')
AND published_at IS NOT NULL
```

**Keyword search (when `q` provided):**

1. `terms = ArabicNormalizer.BuildSearchTerms(q)`
2. If empty after normalize → ignore keyword (or treat as browse-all — document choice)
3. For each term: `normalized_search_text ILIKE '%' + term + '%'`
4. AND all terms

**Date range cap (SPEC 4.4):** Reject or clamp `DateFrom`/`DateTo` outside the last 12 months in the service (not only client date-picker). Out-of-range filter → `400 validation.failed` or silent clamp — document choice in implementation.

**Structural filters (AND with keyword):**

| Filter | Column |
| ------ | ------ |
| Category | `category_id` |
| Governorate | `governorate_id` |
| Type | `type` |
| Date from | `date_lost_or_found >= dateFrom` |
| Date to | `date_lost_or_found <= dateTo` |

**Sort:** `published_at DESC`

**Pagination:** offset/limit; return `totalCount` for page numbers

### Status exclusion test data

Seed or factory helpers for: `Pending Review`, `Rejected`, `Withdrawn` — must never appear in results.

---

## 5. Step-by-step implementation order

1. Define `BrowseFilters` and `PagedResult<T>`
2. Implement base status scope query
3. Add keyword term AND logic using `EF.Functions.ILike`
4. Add structural filters
5. Add sort + pagination
6. Write integration tests with seeded reports
7. Arabic normalization test cases (alef, taa marbuta, tatweel)
8. Do not add controller

---

## 6. Out of scope

- HTTP endpoint (sub-phase 03)
- DTO mapping (sub-phase 04)
- Admin moderation search (Phase 03 — different status scope; shares `ArabicNormalizer` + ILIKE-AND pattern, not `PublicBrowseQueryService`)
- Claim CTA logic

---

## 7. Validation gate

### Automated tests

- [ ] Only `Published` and `Claim In Progress` returned
- [ ] `Pending Review` report excluded
- [ ] Multi-term query: both terms required (AND)
- [ ] `"أحمد"` query matches report with `"احمد"` in search column
- [ ] `"مدرسة"` matches `"مدرسه"` after normalization
- [ ] Tatweel stripped in query matches stored text without tatweel
- [ ] Diacritics stripped in query match plain Arabic stored text
- [ ] `DateFrom` older than 12 months → rejected or clamped (server-side)
- [ ] Category filter combines with keyword (AND)
- [ ] Governorate + type + date range filters work
- [ ] Sort: newer `publishedAt` first
- [ ] Page 2 returns correct offset; `totalCount` accurate
- [ ] Empty result set returns `totalCount = 0`

### Manual smoke checklist

- [ ] Run service in integration test with 5+ seeded reports; inspect SQL via logged query (optional)

---

## 8. Exit criteria

- [ ] All search/filter tests pass (SPEC 15.3 query logic)
- [ ] Service registered in DI
- [ ] Mark sub-phase 02 complete in [phase-04/README.md](./README.md)
