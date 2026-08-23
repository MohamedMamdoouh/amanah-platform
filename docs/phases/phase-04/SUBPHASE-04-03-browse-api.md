# Sub-phase 03 — Browse List API

**Status:** Not started  
**Prerequisites:** [Sub-phase 02 — Browse Query Service](./SUBPHASE-04-02-browse-query-service.md), [Sub-phase 04 — Public DTO](./SUBPHASE-04-04-public-dto.md)

---

## 1. Summary

Expose `GET /api/reports` — the public browse endpoint. No authentication required. Maps query parameters to `BrowseFilters`, calls the browse service, returns paginated public list DTOs.

---

## 2. SPEC references

| SPEC section | Topic |
| ------------ | ----- |
| Section 3 | Logged-out browsing parity |
| Section 4.4 | Browse listing |
| Section 15.3 | Listing scope acceptance criteria |

---

## 3. What you will learn

- Public endpoint with no `[Authorize]`
- Query string binding and validation
- Paginated API response shape for Angular numbered pages
- `claimInProgress` flag on list items

**Files to read after implementing:**

- `api/Controllers/ReportsController.cs` or `PublicReportsController.cs`
- `api/Dtos/Browse/PublicReportListItemDto.cs`
- `api/Dtos/Browse/BrowseListResponse.cs`

---

## 4. Deliverables

### Endpoint

| Method | Route | Auth | Success |
| ------ | ----- | ---- | ------- |
| GET | `/api/reports` | None | `200` paginated list |

### Query parameters

| Param | Maps to |
| ----- | ------- |
| `q` | `BrowseFilters.Query` |
| `category` | Category ID (UUID) |
| `governorate` | Governorate ID |
| `type` | `lost` or `found` |
| `dateFrom` | ISO date |
| `dateTo` | ISO date |
| `page` | Default 1 |
| `pageSize` | Default 20, max 20 |

Invalid UUID → `400`. `pageSize` > 20 → clamp or `400`.

### Response

```json
{
  "items": [
    {
      "id": "...",
      "type": "lost",
      "title": "...",
      "categoryName": "...",
      "governorateName": "...",
      "dateLostOrFound": "2026-01-15",
      "publishedAt": "...",
      "thumbnailUrl": "...",
      "rewardOffered": true,
      "rewardAmount": 500,
      "claimInProgress": false
    }
  ],
  "page": 1,
  "pageSize": 20,
  "totalCount": 42,
  "totalPages": 3
}
```

### List item rules

- `thumbnailUrl`: first **public** photo only; null if private category or no photos
- `claimInProgress`: `true` when `status == Claim In Progress`
- No hidden detail, no phone, no reporter user ID

---

## 5. Step-by-step implementation order

1. Create `PublicReportListItemDto` and `BrowseListResponse`
2. Add `PublicReportMapper.ToListItem` (from sub-phase 04)
3. Add `GetBrowse` action on controller
4. Bind query params → `BrowseFilters`
5. Call `PublicBrowseQueryService.SearchAsync`
6. Map entities → DTOs
7. Integration tests: anonymous access, pagination, filters

---

## 6. Out of scope

- Public detail endpoint (sub-phase 05)
- Angular browse UI (sub-phase 07)
- Claim submission

---

## 7. Validation gate

### Automated tests

- [ ] Anonymous `GET /api/reports` → `200`
- [ ] Response contains only public list fields (no hidden detail key)
- [ ] `claimInProgress: true` for Claim In Progress report
- [ ] Private category list item has `thumbnailUrl: null`
- [ ] `?category={id}` filters correctly
- [ ] `?q=...` keyword search works end-to-end
- [ ] Pagination metadata correct

### Manual smoke checklist

- [ ] `curl` without auth — JSON list returns
- [ ] Same response for logged-in vs logged-out user

---

## 8. Exit criteria

- [ ] Browse API passes all tests
- [ ] Mark sub-phase 03 complete in [phase-04/README.md](./README.md)
