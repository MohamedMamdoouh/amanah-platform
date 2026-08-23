# Sub-phase 04 — Public DTO Mapper

**Status:** Not started  
**Prerequisites:** Phase 02 report entities, Phase 03 approval flow (`publishedAt`)

---

## 1. Summary

Implement `PublicReportMapper` — projects `Report` entities to public-safe DTOs. Strips hidden verification detail, private photos, and phone numbers. Adds `claimInProgress` flag. Pure mapping layer with unit tests — no new endpoints.

---

## 2. SPEC references

| SPEC section | Topic |
| ------------ | ----- |
| Section 9 | Public visibility matrix |
| Section 4.4 | Public fields on listings |
| Section 5.2 | Private photos never on public listing |

---

## 3. What you will learn

- Defense in depth: even if query leaks a row, mapper must not expose private fields
- Public photo URL vs omitting private photos entirely
- Which fields appear on list vs detail DTOs
- `claimInProgress` derived from status enum

**Files to read after implementing:**

- `api/Mapping/PublicReportMapper.cs`
- `api/Dtos/Browse/PublicReportListItemDto.cs`
- `api/Dtos/Browse/PublicReportDetailDto.cs`
- `api.Tests/Mapping/PublicReportMapperTests.cs`

---

## 4. Deliverables

### `PublicReportListItemDto`

| Field | Source |
| ----- | ------ |
| `id`, `type`, `title` | Report |
| `categoryName`, `governorateName` | Navigation |
| `dateLostOrFound`, `publishedAt` | Report |
| `thumbnailUrl` | First public photo only |
| `rewardOffered`, `rewardAmount` | Report |
| `claimInProgress` | `status == ClaimInProgress` |

### `PublicReportDetailDto`

All list fields plus:

| Field | Notes |
| ----- | ----- |
| `description` | Full text |
| `area` | Area/landmark |
| `categoryFields` | Public field key/value pairs |
| `photos` | Public photos only — `{ id, url, thumbnailUrl }` |
| `itemHeldLocation`, `itemHeldLocationDetail` | Found reports only |
| `reporterDisplayName` | Display name only — **not** phone |
| `claimInProgress` | Boolean |
| `status` | `Published` or `ClaimInProgress` only for public 200 responses |

### Fields **never** in public DTOs

- `hiddenVerificationDetail`
- Private category photos
- Reporter phone / user ID
- `rejectionReason`, withdrawal reason
- Admin-only fields

### `PublicReportMapper` methods

| Method | Returns |
| ------ | ------- |
| `ToListItem(Report)` | `PublicReportListItemDto` |
| `ToDetail(Report)` | `PublicReportDetailDto` |

### Photo URL rules

- Public photos: stable public URL or CDN path
- Private photos: **omit** from `photos` array — do not include placeholder

---

## 5. Step-by-step implementation order

1. Define list and detail DTOs
2. Implement `ToListItem` with thumbnail logic
3. Implement `ToDetail` with full field set
4. Unit test: report with private photos → empty `photos` array
5. Unit test: mapper output JSON has no `hiddenVerificationDetail` property
6. Unit test: `claimInProgress` true/false
7. Register mapper (static class or injectable service — match project style)

---

## 6. Out of scope

- HTTP endpoints (sub-phases 03, 05)
- Reporter/admin DTOs (Phase 02/03)
- Claim button logic (Angular sub-phase 08)

---

## 7. Validation gate

### Automated tests

- [ ] Public photos included in detail `photos`
- [ ] `photosPrivate` category → `photos` empty, `thumbnailUrl` null on list
- [ ] Hidden detail never mapped
- [ ] Reporter display name included; phone never included
- [ ] Found report includes held location fields
- [ ] Lost report has null held location fields

### Manual smoke checklist

- [ ] Read mapper file top-to-bottom; confirm no accidental field leaks

---

## 8. Exit criteria

- [ ] Mapper unit tests pass
- [ ] Used by browse API (sub-phase 03) and detail API (sub-phase 05)
- [ ] Mark sub-phase 04 complete in [phase-04/README.md](./README.md)
