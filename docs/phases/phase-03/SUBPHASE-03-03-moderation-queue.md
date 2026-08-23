# Sub-phase 03 — Moderation Queue & Admin Detail

**Status:** Not started  
**Prerequisites:** Phase 01 admin role ([Sub-phase 11](../phase-01/SUBPHASE-01-11-auth-ui-admin.md) for role guard pattern), Phase 02 report create

---

## 1. Summary

Implement admin read endpoints: FIFO moderation queue with pending count, and full report detail for review including private photos. Admin role required. Hidden verification detail is **never** returned.

---

## 2. SPEC references

| SPEC section | Topic |
| ------------ | ----- |
| Section 4.3 | FIFO queue, oldest first |
| Section 5.5 | Admin review surfaces |
| Section 9 | Private photos for admin during review; hidden detail never |

---

## 3. What you will learn

- Admin authorization policy (`[Authorize(Roles = "Admin")]`)
- FIFO ordering: `ORDER BY created_at ASC` for queue
- Admin DTO projection — same as reporter detail minus hidden field
- Pending count for badge (returned in queue response header or body)

**Files to read after implementing:**

- `api/Controllers/Admin/ModerationController.cs` (read actions only)
- `api/Services/Moderation/ModerationQueueService.cs`
- `api/Dtos/Moderation/ModerationQueueItemDto.cs`
- `api/Dtos/Moderation/AdminReportDetailDto.cs`

---

## 4. Deliverables

### Endpoints

| Method | Route | Auth | Purpose |
| ------ | ----- | ---- | ------- |
| GET | `/api/admin/moderation/queue` | Admin | FIFO pending reports |
| GET | `/api/admin/moderation/reports/{id}` | Admin | Full review detail |

### `GET /api/admin/moderation/queue`

**Query:** `page` (default 1), `pageSize` (default 20, max 50)

**Filter:** `status = Pending Review` only

**Sort:** `createdAt` ascending (oldest first — FIFO)

**Response:**

```json
{
  "pendingCount": 12,
  "items": [
    {
      "id": "...",
      "type": "lost",
      "title": "...",
      "categoryName": "...",
      "reporterDisplayName": "...",
      "createdAt": "...",
      "thumbnailUrl": "..."
    }
  ],
  "page": 1,
  "pageSize": 20,
  "totalCount": 12
}
```

`pendingCount` = total pending (not just current page) — for nav badge.

### `GET /api/admin/moderation/reports/{id}`

**Access:** Admin only; report must exist

**Includes:**

- All public report fields (title, description, dates, location, category fields, reward, held location)
- Reporter display name (not phone unless admin user lookup — out of scope)
- All photos with URLs (presigned for private)
- Category `photosPrivate` flag

**Excludes:**

- `hiddenVerificationDetail` — **never**, even for admin
- Assert in unit test: admin mapper has no hidden detail property

**Status:** `Pending Review`, `Rejected`, and `Published` (for search-result deep links). Return `404` for missing report or statuses outside admin review scope (e.g. terminal).

### Authorization

- Non-admin → `403`
- Register `AdminOnly` policy if not already present

---

## 5. Step-by-step implementation order

1. Create `ModerationQueueService` with FIFO query
2. Build `ModerationQueueItemDto` mapper
3. Build `AdminReportDetailDto` mapper — **explicitly omit** hidden detail
4. Add `ModerationController` with `GetQueue` and `GetReport`
5. Write test: admin sees queue; reporter gets `403`
6. Write test: admin detail JSON has no `hiddenVerificationDetail` key
7. Write test: private category photos return presigned URLs

---

## 6. Out of scope

- Approve/reject actions (sub-phase 04)
- `ModerationAction` writes (sub-phase 04)
- Search (sub-phase 06)
- Angular admin UI (sub-phase 09)

---

## 7. Validation gate

### Automated tests

- [ ] Queue returns only `Pending Review` reports
- [ ] Oldest report appears first
- [ ] `pendingCount` matches DB count
- [ ] Non-admin → `403`
- [ ] Admin detail includes private photos for Documents/IDs category
- [ ] Admin detail JSON does not contain hidden verification field
- [ ] Unauthenticated → `401`

### Manual smoke checklist

- [ ] Seed 3 pending reports with different `createdAt`; queue order is oldest-first
- [ ] Open admin detail for Documents/IDs — photos visible to admin

---

## 8. Exit criteria

- [ ] Queue and detail endpoints pass all tests
- [ ] Hidden detail exclusion verified by automated test
- [ ] Mark sub-phase 03 complete in [phase-03/README.md](./README.md)
