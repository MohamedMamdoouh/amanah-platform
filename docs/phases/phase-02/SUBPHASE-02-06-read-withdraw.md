# Sub-phase 06 — Report Read & Withdraw API

**Status:** Not started  
**Prerequisites:** [Sub-phase 05 — Report Create](./SUBPHASE-02-05-report-create.md)

---

## 1. Summary

Implement reporter-facing read endpoints and withdraw action: list own reports, view detail (with permission-aware field filtering), and withdraw while `Pending Review`. Enforces Section 9 permissions — especially hidden detail visible only to reporter, never admin.

---

## 2. SPEC references

| SPEC section | Topic |
| ------------ | ----- |
| Section 4.8 | My Reports — Pending Review tab (other tabs stub OK) |
| Section 4.7 | Withdraw while Pending Review |
| Section 9 | Permission matrix |
| Section 15.1 | Reporter can withdraw pending report |

---

## 3. What you will learn

- Role-based DTO projection (reporter vs admin vs public)
- Why non-reporters get `404` (not `403`) for pending reports
- Withdrawal: status transition `Pending Review` → `Withdrawn`, optional reason field
- Listing photos in detail: public URLs vs presigned for private

**Files to read after implementing:**

- `api/Controllers/ReportsController.cs` (list, get, withdraw)
- `api/Services/Reports/ReportQueryService.cs`
- `api/Dtos/Reports/ReportDetailDto.cs`, `ReportListItemDto.cs`
- `api.Tests/Controllers/ReportsControllerReadTests.cs`

---

## 4. Deliverables

### Endpoints

| Method | Route | Auth | Purpose |
| ------ | ----- | ---- | ------- |
| GET | `/api/reports/mine` | Required | Reporter's reports |
| GET | `/api/reports/{id}` | Required | Report detail |
| POST | `/api/reports/{id}/withdraw` | Required | Withdraw pending report |

### `GET /api/reports/mine`

**Query:** `status` (optional) — for Phase 02, support `pending-review` tab only; other status filters return `400` until Phase 03 extends My Reports tabs.

**Response:** array of list items: `id`, `type`, `status`, `title`, `categoryName`, `createdAt`, `thumbnailUrl` (first public photo or null for private-only).

Sorted by `createdAt` descending.

### `GET /api/reports/{id}`

**Access rules:**

| Viewer | Pending Review | Withdrawn | Notes |
| ------ | -------------- | --------- | ----- |
| Reporter (owner) | ✓ Full detail incl. hidden detail | ✓ Full detail incl. hidden detail + withdrawal reason | |
| Admin | ✓ Detail **without** hidden detail | ✓ Detail **without** hidden detail + withdrawal reason | |
| Other logged-in user | `404` | `404` | |
| Anonymous | `401` | `401` | Auth required on all routes |

**Detail fields for reporter:** all report fields, category field values, photos (with URL resolution), reward, held location, hidden verification detail.

**Detail fields for admin:** same except **no** `hiddenVerificationDetail`. Private photos via presigned URL OK.

### `POST /api/reports/{id}/withdraw`

**Rules:**

- Reporter only
- Status must be `Pending Review` → else `409 report.invalid_status`
- Body: optional `reason` enum (`RecoveredOutside`, `NoLongerNeeded`, `PostedByMistake`, `Other`)
- Sets status `Withdrawn`, stores reason, `WithdrawnAt` timestamp
- `withdrawalReason` returned only to reporter and admin on detail (SPEC §9); omitted from list items and all non-owner responses

**Out of scope for Phase 02:** withdraw while `Published` (Phase 07)

---

## 5. Step-by-step implementation order

1. Implement `ReportQueryService` with viewer-role projection
2. Add `GetMine` and `GetById` to `ReportsController`
3. Create separate mapper methods: `ToReporterDetail`, `ToAdminDetail`
4. Assert admin mapper never maps hidden detail (unit test the mapper)
5. Implement `WithdrawAsync` in service
6. Write integration tests for all access combinations
7. Test withdraw transitions status correctly

---

## 6. Out of scope

- Rejected / Published / Claims tabs (Phases 03–05)
- Public browse URLs `/lost/{id}` (Phase 04)
- Admin moderation queue (Phase 03)
- Angular My Reports UI (sub-phase 10)

---

## 7. Validation gate

### Automated tests — list

- [ ] Reporter sees only own reports
- [ ] Filter `status=pending-review` returns correct subset
- [ ] Private category report list item has no public thumbnail URL

### Automated tests — detail

- [ ] Reporter sees `hiddenVerificationDetail`
- [ ] Admin does **not** see `hiddenVerificationDetail`
- [ ] Other user → `404` for pending report
- [ ] Private photos return presigned URL for reporter and admin

### Automated tests — withdraw

- [ ] Reporter withdraws pending report → `200`, status `Withdrawn`
- [ ] Second withdraw → `409`
- [ ] Non-owner withdraw → `404`
- [ ] Admin cannot withdraw → `403`

### Manual smoke checklist

- [ ] Create report as user A; fetch as admin — confirm hidden field absent in JSON
- [ ] Withdraw and verify `GET /api/reports/mine` no longer shows it in pending filter

---

## 8. Exit criteria

- [ ] All automated tests pass
- [ ] Permission matrix rows for Phase 02 enforced server-side
- [ ] Mark sub-phase 06 complete in [phase-02/README.md](./README.md)
