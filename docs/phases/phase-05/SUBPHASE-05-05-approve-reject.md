# Sub-phase 05 — Approve & Reject Claims

**Status:** Not started  
**Prerequisites:** [Sub-phase 04](./SUBPHASE-05-04-claim-create.md), Phase 03 [Notifications](../phase-03/SUBPHASE-03-02-notifications-api.md)

---

## 1. Summary

Implement reporter approve/reject: `POST /api/claims/{id}/approve` and `POST /api/claims/{id}/reject`. On approve: report → `Claim In Progress`, create `ChatThread` placeholder, auto-reject other pending claims. Notifications for approve, reject, and auto-reject.

---

## 2. SPEC references

| SPEC section | Topic |
| ------------ | ----- |
| Section 6.3–6.5 | Approval side effects |
| Section 8 | `Published` → `Claim In Progress` |
| Section 15.4 | Approval and attempt counting |

---

## 3. What you will learn

- Transaction: approve one claim + update report + create thread + auto-reject others
- `ChatThread` linked to `Claim` — **no hub, no messages**
- Manual reject sets `countsAsAttempt = true`
- Auto-reject sets `countsAsAttempt = false`, reason `AnotherClaimApproved`

**Files to read after implementing:**

- `api/Services/Claims/ClaimReviewService.cs`
- `api/Controllers/ClaimsController.cs` (approve/reject)
- `api/Data/Entities/ChatThread.cs`

---

## 4. Deliverables

### Endpoints

| Method | Route | Auth | Success |
| ------ | ----- | ---- | ------- |
| POST | `/api/claims/{id}/approve` | Reporter (report owner) | `200` |
| POST | `/api/claims/{id}/reject` | Reporter | `200` |

### Approve flow

1. Verify reporter owns the report
2. Claim must be `Pending`; report must be `Published`
3. **Transaction:**
   - Claim → `Approved`
   - Report → `Claim In Progress`
   - Create `ChatThread` (claimId, createdAt) — add code comment: *Phase 06 activates*
   - For each other `Pending` claim on report: → `Rejected`, reason `AnotherClaimApproved`, `countsAsAttempt = false`; notify each claimant (`ClaimRejected`)
   - Notify approved claimant: `ClaimApproved`
4. Return updated claim

### Reject flow

1. Verify reporter; claim `Pending`
2. Claim → `Rejected`, reason `ReporterRejected`, **`countsAsAttempt = true`**
3. Notify claimant: `ClaimRejected`
4. Report stays `Published`

### `GET /api/reports/{id}/claims` (reporter only)

List all claims on own report: id, status, claimant display name, submittedAt, answer preview (truncated), hasPhoto.

---

## 5. Step-by-step implementation order

1. Implement `ClaimReviewService.ApproveAsync`
2. Implement `ClaimReviewService.RejectAsync`
3. Implement auto-reject loop + notifications
4. Add `ChatThread` creation with prominent Phase 06 comment
5. Add list endpoint on report
6. Integration tests: approve, reject, auto-reject, thread exists, no messages table rows

---

## 6. Out of scope

- SignalR / chat UI (Phase 06)
- Cancel approved claim (Phase 06)
- Claimant withdraw (sub-phase 06)

---

## 7. Validation gate

- [ ] Approve → report `Claim In Progress`, thread row created
- [ ] Other pending claims auto-rejected, no attempt consumed
- [ ] Manual reject → attempt counted on that claim
- [ ] Non-reporter cannot approve → `403`/`404`
- [ ] `ClaimApproved` / `ClaimRejected` notifications sent
- [ ] No `Message` rows created

---

## 8. Exit criteria

- [ ] Approve/reject flows pass SPEC 15.4 approval criteria
- [ ] Mark sub-phase 05 complete in [phase-05/README.md](./README.md)
