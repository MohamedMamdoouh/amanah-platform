# Sub-phase 01 — Claim Decisions & Domain Types

**Status:** Not started  
**Prerequisites:** Phase 04 complete

---

## 1. Summary

Define claim domain enums, attempt-counting rules, auto-rejection reason, and notification type constants. Document the Phase 05/06 boundary for `ChatThread`. No endpoints — types and decisions only.

---

## 2. SPEC references

| SPEC section | Topic |
| ------------ | ----- |
| Section 6 | Claim statuses, attempt model |
| Section 5.7 | Claim notification types |
| Section 8 | Status transitions |

---

## 3. What you will learn

- `ClaimStatus` enum and when `countsAsAttempt` is set
- System rejection reason `Another claim approved` (not in admin rejection list)
- 3 lifetime attempts per user per report — what counts vs exempt

**Files to read after implementing:**

- `api/Domain/Claims/ClaimStatus.cs`
- `api/Domain/Claims/ClaimRejectionReason.cs`
- `api/Domain/Notifications/NotificationTypes.cs` (claim events)

---

## 4. Deliverables

### `ClaimStatus` enum

`Pending`, `Approved`, `Rejected`, `Withdrawn`, `Cancelled`

### `ClaimRejectionReason` (system + manual)

| Value | When |
| ----- | ---- |
| `ReporterRejected` | Manual reporter reject — **counts as attempt** |
| `AnotherClaimApproved` | Auto-reject — **does not** count |
| `ReportUnavailable` | Report withdrawn/expired/takedown — **does not** count |

### Attempt counting rules (document in code comments)

| Action | Counts? |
| ------ | ------- |
| Reporter manual reject | Yes |
| Claimant cancels approved claim (Phase 06) | Yes |
| Reporter cancels approved claim (Phase 06) | No |
| Claimant withdraws pending | No |
| Auto-reject (another approved) | No |
| Refused (wrong status, duplicate pending) | No |
| Auto-withdraw (Phase 07) | No |

### Notification types (Phase 05)

`NewClaimSubmitted`, `ClaimWithdrawnByClaimant`, `ClaimApproved`, `ClaimRejected`, `ClaimClosedReportUnavailable`

### `ChatThread` placeholder comment

Add XML doc on `ChatThread` entity: *"Created in Phase 05; messaging activated in Phase 06."*

---

## 5. Step-by-step implementation order

1. Read SPEC Section 6.4 attempt counting
2. Create enum files
3. Add notification type constants
4. Verify `Claim` entity has: `attemptNumber`, `countsAsAttempt`, `submittedAt`, `status`
5. No service or controller code

---

## 6. Out of scope

- API endpoints
- Chat messaging (Phase 06)

---

## 7. Validation gate

- [ ] All enum values serialize correctly
- [ ] Attempt rules documented in `ClaimAttemptPolicy.cs` or comments
- [ ] `ChatThread` entity reviewed

---

## 8. Exit criteria

- [ ] Domain types committed
- [ ] Mark sub-phase 01 complete in [phase-05/README.md](./README.md)
