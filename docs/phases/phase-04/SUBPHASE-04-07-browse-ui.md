# Sub-phase 07 — Browse Page UI

**Status:** Not started  
**Prerequisites:** [Sub-phase 03 — Browse API](./SUBPHASE-04-03-browse-api.md), [Sub-phase 06 — Static Routes](./SUBPHASE-04-06-static-routes.md), Phase 02 reference data services

---

## 1. Summary

Build `/browse` — public search, filters, report card grid, and numbered pagination. Works logged-out and logged-in with identical content. Entry point for discovery.

---

## 2. SPEC references

| SPEC section | Topic |
| ------------ | ----- |
| Section 3 | Logged-out browse parity |
| Section 4.4 | Search, filters, pagination |
| Section 11 | RTL layout |

---

## 3. What you will learn

- Public page with no auth guard
- Debounced search input → `q` query param
- Filter chips/dropdowns synced to URL query params (shareable browse URLs)
- Report card component with `claimInProgress` badge
- Numbered pagination component (20 per page)

**Files to read after implementing:**

- `web/src/app/features/browse/browse-page/`
- `web/src/app/features/browse/browse.service.ts`
- `web/src/app/features/browse/report-card/`
- `web/src/app/features/browse/browse-filters/`

---

## 4. Deliverables

### Page: `/browse`

**Layout (RTL):**

1. Search bar (keyword `q`) — debounce 300ms
2. Filter row:
   - Category dropdown (from `CategoriesService`)
   - Governorate dropdown (from `GovernoratesService`)
   - Type toggle: الكل / مفقود / موجود
   - Date range (from/to date pickers, max 12 months)
3. Results count: "عرض {n} من {total} بلاغ"
4. Report card grid (responsive)
5. Numbered pagination at bottom

### Report card

| Element | Source |
| ------- | ------ |
| Thumbnail | `thumbnailUrl` or placeholder |
| Title | Linked to `/lost/{id}` or `/found/{id}` |
| Category + governorate | |
| Date lost/found | |
| Reward badge | If `rewardOffered` |
| Claim in progress badge | If `claimInProgress` — distinct styling |

### `BrowseService`

| Method | API |
| ------ | --- |
| `search(filters)` | `GET /api/reports?...` |

### URL sync

Browse state in query params: `/browse?q=هاتف&category=...&page=2`

- Back button restores filters
- Shareable URL

### Empty states

- No results: "لا توجد نتائج — جرّب تعديل البحث أو الفلاتر"
- Initial load: skeleton cards

### Auth

- No login required
- Header may show login button (Phase 01) — no change to browse content

---

## 5. Step-by-step implementation order

1. Create `BrowseService` with typed filter model
2. Build `ReportCardComponent`
3. Build `BrowseFiltersComponent` (dropdowns + date range)
4. Build `BrowsePageComponent` — wire search debounce
5. Sync filters to URL query params (`Router` + `queryParams`)
6. Add pagination component
7. Handle `claimInProgress` badge styling
8. Test logged-out in browser
9. Test Arabic search from UI

---

## 6. Out of scope

- Detail pages (sub-phase 08)
- Claim button on cards (detail only in sub-phase 08)
- Map view
- Infinite scroll (v1 uses numbered pages per SPEC)

---

## 7. Validation gate

### Automated tests

- [ ] `BrowseService` builds correct query string
- [ ] Filter change updates URL params
- [ ] Report card links to correct `/lost` or `/found` URL
- [ ] Claim in progress badge shown when flag true

### Manual smoke checklist

- [ ] Browse logged-out — list loads
- [ ] Search Arabic keyword — results filter
- [ ] Combine category + governorate filters
- [ ] Pagination page 2 loads different items
- [ ] URL copy-paste in new tab preserves filters
- [ ] Claim In Progress report shows badge

---

## 8. Exit criteria

- [ ] Browse page works end-to-end against local API
- [ ] Mark sub-phase 07 complete in [phase-04/README.md](./README.md)
