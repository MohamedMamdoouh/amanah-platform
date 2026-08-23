# Sub-phase 04 — Approve & Reject Actions

**Status:** Not started  
**Prerequisites:** [Sub-phase 02 — Notifications API](./SUBPHASE-03-02-notifications-api.md), [Sub-phase 03 — Moderation Queue](./SUBPHASE-03-03-moderation-queue.md)

---

## 1. Summary

Implement approve and reject actions: status transitions, `ModerationAction` audit records, and in-app notifications to the reporter. Core moderation workflow — read every line of the transaction boundaries.

---

## 2. SPEC references

| SPEC section | Topic |
| ------------ | ----- |
| Section 4.3 | Approve → Published; reject → Rejected |
| Section 5.5 | Rejection reasons and optional note |
| Section 5.7 | `ReportApproved`, `ReportRejected` events |
| Section 8 | Status transition table |
| Section 12 | `ModerationAction` survives report deletion |
| Section 15.2 | Approve and reject acceptance criteria |

---

## 3. What you will learn

- Atomic status transition + audit insert + notification create
- Setting `publishedAt` on first approval
- Storing rejection reason and note on `Report` (for reporter UI)
- Why rejected reports leave the queue but remain in DB
- Idempotency: cannot approve already-published report

**Files to read after implementing:**

- `api/Services/Moderation/ModerationDecisionService.cs`
- `api/Controllers/Admin/ModerationController.cs` (approve/reject actions)
- `api/Data/Entities/ModerationAction.cs`
- `api/Data/Entities/Report.cs` — rejection fields if added

---

## 4. Deliverables

### Endpoints

| Method | Route | Body | Success |
| ------ | ----- | ---- | ------- |
| POST | `/api/admin/moderation/reports/{id}/approve` | none | `200` |
| POST | `/api/admin/moderation/reports/{id}/reject` | `{ reason, note? }` | `200` |

### Approve flow

1. Verify admin role
2. Load report; status must be `Pending Review` → else `409 report.invalid_status`
3. **Transaction:**
   - `Status` → `Published`
   - `PublishedAt` → `UtcNow`
   - Insert `ModerationAction` (`Decision = Approved`, `AdminUserId`, `ReportId`)
   - `NotificationService.CreateAsync(reporterId, ReportApproved, payload)`
4. Return updated report summary (admin DTO)

### Reject flow

1. Verify admin role
2. Load report; status must be `Pending Review`
3. Validate `reason` — required, must be valid `RejectionReason` enum
4. Validate `note` — optional, max 500 chars
5. **Transaction:**
   - `Status` → `Rejected`
   - Store `RejectionReason`, `RejectionNote`, `RejectedAt` on report
   - Insert `ModerationAction` with reason + note
   - `NotificationService.CreateAsync(reporterId, ReportRejected, payload with reason + note)`
6. Return updated report summary

### Notification payloads

**ReportApproved:**

```json
{
  "type": "ReportApproved",
  "createdAt": "...",
  "deepLink": "/my/reports/{id}",
  "reportId": "..."
}
```

**ReportRejected:**

```json
{
  "type": "ReportRejected",
  "createdAt": "...",
  "deepLink": "/my/reports/{id}",
  "reportId": "...",
  "rejectionReason": "UnclearPhotos",
  "rejectionNote": "optional admin note"
}
```

### Report entity additions (migration if needed)

| Field | Purpose |
| ----- | ------- |
| `RejectionReason` | Last rejection reason (nullable) |
| `RejectionNote` | Optional note to reporter |
| `RejectedAt` | Timestamp of last rejection |

### `ModerationAction` persistence test

Sub-phase exit must verify: delete report → `ModerationAction` row remains with `ReportId` set null or retained per SPEC (nullable FK).

---

## 5. Step-by-step implementation order

1. Add rejection fields to `Report` if not in Phase 01 schema (migration)
2. Implement `ModerationDecisionService.ApproveAsync`
3. Implement `ModerationDecisionService.RejectAsync`
4. Wire `INotificationService` in both flows
5. Add controller actions
6. Integration tests: approve, reject, invalid status, notification created
7. Test `ModerationAction` survives simulated report deletion

---

## 6. Out of scope

- Public browse listing (Phase 04) — verify `Published` status via API/DB only
- Resubmit flow (sub-phase 07)
- Admin email (sub-phase 05)
- Angular UI (sub-phase 09)

---

## 7. Validation gate

### Automated tests

- [ ] Approve pending report → `Published`, `publishedAt` set
- [ ] Approve creates `ModerationAction` with `Approved`
- [ ] Approve creates `ReportApproved` notification for reporter
- [ ] Reject with reason + note → `Rejected`, fields stored
- [ ] Reject creates `ReportRejected` notification with reason in payload
- [ ] Reject removes report from queue (`GET queue` no longer includes it)
- [ ] Approve already-published → `409`
- [ ] Reject with invalid reason → `400`
- [ ] Non-admin → `403`
- [ ] `ModerationAction` persists after report deletion

### Manual smoke checklist

- [ ] Approve via Swagger; reporter notification appears in `GET /api/notifications`
- [ ] Reject with Arabic note; note visible in reporter detail (sub-phase 07/10)

---

## 8. Exit criteria

- [ ] Approve/reject flows pass SPEC 15.2 API criteria
- [ ] Audit trail and notifications verified
- [ ] Mark sub-phase 04 complete in [phase-03/README.md](./README.md)
