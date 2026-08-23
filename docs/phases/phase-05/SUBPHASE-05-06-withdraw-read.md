# Sub-phase 06 — Withdraw & Claim Read APIs

**Status:** Not started  
**Prerequisites:** [Sub-phase 05](./SUBPHASE-05-05-approve-reject.md)

---

## 1. Summary

Implement claimant withdraw, My Claims list, and claim detail endpoints. Withdraw does not consume an attempt and notifies the reporter.

---

## 2. SPEC references

| SPEC section | Topic |
| ------------ | ----- |
| Section 6.2 | Claimant withdrawal |
| Section 4.8 | My Claims |
| Section 9 | Claim visibility |

---

## 3. What you will learn

- Withdraw only while `Pending`
- Claim detail DTO: claimant sees own; reporter sees for review (incl. photo URL)
- My Claims list with status and deep links

**Files to read after implementing:**

- `api/Services/Claims/ClaimQueryService.cs`
- `api/Controllers/ClaimsController.cs` (withdraw, mine, get)

---

## 4. Deliverables

### Endpoints

| Method | Route | Auth |
| ------ | ----- | ---- |
| POST | `/api/claims/{id}/withdraw` | Claimant |
| GET | `/api/claims/mine` | Required |
| GET | `/api/claims/{id}` | Claimant or reporter |

Admin access to claim text on this endpoint: return `404` with a documented TODO until Phase 08 investigation mode (same stub pattern as claim-photo URL in sub-phase 03). Phase 08 investigation API returns claim text during flagged-listing review.

### Withdraw

- Status `Pending` only → else `409`
- → `Withdrawn`, `countsAsAttempt = false`
- Notify reporter: `ClaimWithdrawnByClaimant`

### `GET /api/claims/mine`

Paginated list: report title, type, status, submittedAt, deepLink to report.

### `GET /api/claims/{id}`

Full answer, photo presigned URL (if any), status, attempt info, report summary. Reporter sees claimant display name; claimant sees reporter display name.

---

## 5. Step-by-step implementation order

1. Implement withdraw in service
2. Implement query service + DTOs
3. Add controller actions
4. Tests: withdraw, mine list, detail access control

---

## 6. Out of scope

- Cancel approved claim (Phase 06)
- Chat access (Phase 06)

---

## 7. Validation gate

- [ ] Withdraw pending → `Withdrawn`, no attempt counted, notification sent
- [ ] Withdraw approved → `409`
- [ ] Claimant sees mine list; other user cannot see claim detail
- [ ] Reporter can view claim detail for own report

---

## 8. Exit criteria

- [ ] All read/withdraw APIs tested
- [ ] Mark sub-phase 06 complete in [phase-05/README.md](./README.md)
