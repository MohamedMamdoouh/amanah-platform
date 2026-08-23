# Sub-phase 09 — Admin Moderation UI

**Status:** Not started  
**Prerequisites:** API sub-phases [03](./SUBPHASE-03-03-moderation-queue.md)–[06](./SUBPHASE-03-06-moderation-search.md), Phase 01 admin role guard

---

## 1. Summary

Build `/admin/moderation` FIFO queue with pending-count badge and `/admin/moderation/{id}` review page with approve/reject actions. Admin role guard required. This is the primary admin workflow — read every component and service line.

---

## 2. SPEC references

| SPEC section | Topic |
| ------------ | ----- |
| Section 4.3 | FIFO queue, approve/reject |
| Section 5.5 | Admin moderation surfaces |
| Section 9 | Private photos visible to admin; hidden detail never |

---

## 3. What you will learn

- Admin layout and navigation (extend Phase 01 admin shell)
- FIFO list UX — oldest at top, pending badge in nav
- Reject dialog with reason dropdown (8 options) + optional note textarea
- Approve confirmation (simple confirm dialog)
- Keyword search box wired to moderation search API
- Displaying private photos in admin review

**Files to read after implementing:**

- `web/src/app/features/admin/moderation/moderation-queue-page/`
- `web/src/app/features/admin/moderation/moderation-detail-page/`
- `web/src/app/features/admin/moderation/moderation.service.ts`
- `web/src/app/features/admin/moderation/reject-dialog/`

---

## 4. Deliverables

### Page: `/admin/moderation`

- Pending count badge in admin nav (from queue API `pendingCount`)
- Table/cards: title, type, category, reporter name, submitted date
- **FIFO order** — oldest first (match API sort)
- Pagination
- Search input → calls `GET /api/admin/moderation/search?q=...`
- Click row → `/admin/moderation/{id}`
- Empty state when queue clear

### Page: `/admin/moderation/{id}`

- Full report detail (all fields except hidden verification — API never sends it)
- Photo gallery with lightbox; private photos load via presigned URLs
- **Approve** button → confirm → `POST approve` → navigate back to queue with success toast
- **Reject** button → opens reject dialog:
  - Reason dropdown (8 Arabic labels from enum)
  - Optional note (max 500 chars)
  - Submit → `POST reject` → back to queue
- Error handling: already processed report → show message

### `ModerationService`

| Method | API |
| ------ | --- |
| `getQueue(page)` | `GET /api/admin/moderation/queue` |
| `getReport(id)` | `GET /api/admin/moderation/reports/{id}` |
| `search(q, page)` | `GET /api/admin/moderation/search` |
| `approve(id)` | `POST .../approve` |
| `reject(id, reason, note?)` | `POST .../reject` |

### Guards

- `adminGuard` on `/admin/moderation` and `/admin/moderation/:id`
- Non-admin → redirect or 403 page

---

## 5. Step-by-step implementation order

1. Create `ModerationService`
2. Build queue page with FIFO list and pagination
3. Add pending badge to admin nav (shared layout)
4. Build detail page — read-only field display
5. Wire photo gallery with presigned URL refresh on error
6. Add approve flow with confirmation
7. Build reject dialog component
8. Add search box on queue page
9. Manual E2E: submit report as user → approve as admin → verify queue updates

---

## 6. Out of scope

- Category management UI (sub-phase 11)
- Abuse queue (Phase 08)
- User ban/lookup (Phase 08)
- Reporter notification UI (sub-phase 10)
- Public browse (Phase 04)

---

## 7. Validation gate

### Automated tests

- [ ] Queue component renders items from mock service
- [ ] Reject dialog requires reason selection
- [ ] `adminGuard` blocks non-admin route access
- [ ] Approve calls service and navigates on success

### Manual smoke checklist

- [ ] Admin sees FIFO queue with correct pending badge count
- [ ] Review Documents/IDs report — private photos visible
- [ ] No hidden verification section on detail page (cannot exist — API omits it)
- [ ] Approve report; disappears from queue
- [ ] Reject with reason + note; report removed from queue
- [ ] Search finds pending report by title keyword

---

## 8. Exit criteria

- [ ] Full admin approve/reject workflow works end-to-end
- [ ] Mark sub-phase 09 complete in [phase-03/README.md](./README.md)
