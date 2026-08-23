# Sub-phase 04 — Claim Create API

**Status:** Not started  
**Prerequisites:** [Sub-phases 02](./SUBPHASE-05-02-validation-quota.md), [03](./SUBPHASE-05-03-claim-photo.md)

---

## 1. Summary

Implement `POST /api/reports/{id}/claims` — submit a claim on a `Published` report. Enforces all creation constraints and sends `NewClaimSubmitted` notification to reporter.

---

## 2. SPEC references

| SPEC section | Topic |
| ------------ | ----- |
| Section 6.2 | Claim creation |
| Section 6.5 | Only on `Published` |
| Section 15.4 | Creation constraints |

---

## 3. What you will learn

- Refusing self-claims (claimant ≠ reporter)
- One open `Pending` claim per user per report
- Refused claims on wrong status do not consume attempts
- Optional `photoId` link (max 1)

**Files to read after implementing:**

- `api/Services/Claims/ClaimSubmissionService.cs`
- `api/Controllers/ClaimsController.cs` (create only)
- `api/Dtos/Claims/CreateClaimRequest.cs`

---

## 4. Deliverables

### Endpoint

| Method | Route | Body | Success |
| ------ | ----- | ---- | ------- |
| POST | `/api/reports/{id}/claims` | `{ answer, photoId? }` | `201` |

### Create flow

1. Authenticate
2. Load report — must be `Published` → else `409` **without** consuming attempt
3. User cannot be reporter → else `409 claim.cannot_claim_own_report`
4. `ClaimAttemptService.CanSubmit` → else `409 claim.attempt_limit_exceeded`
5. No existing `Pending` claim by this user on this report → else `409 claim.duplicate_pending`
6. `ClaimQuotaService` → else `429 claim.quota_daily_exceeded`
7. Validate answer text (sub-phase 02)
8. Validate `photoId` if provided — owned by user, unlinked
9. Insert `Claim`: `Pending`, `submittedAt`, `attemptNumber` = next sequence, `countsAsAttempt = false`
10. Notify reporter: `NewClaimSubmitted`
11. Return `201` + claim summary

### Error codes

Document in [api-error-contract.md](../../api-error-contract.md) if missing.

---

## 5. Step-by-step implementation order

1. Define request/response DTOs
2. Implement `ClaimSubmissionService.CreateAsync`
3. Wire validators and quota services
4. Add controller action
5. Integration tests: happy path, all refusal cases
6. Verify refused wrong-status does not increment attempt count

---

## 6. Out of scope

- Approve/reject (sub-phase 05)
- Claim UI (sub-phase 08)

---

## 7. Validation gate

- [ ] Valid claim on Published → `201`, notification created
- [ ] Claim In Progress report → `409`, no attempt consumed
- [ ] Self-claim → `409`
- [ ] Duplicate pending → `409`
- [ ] 6th claim today → `429`
- [ ] Contact-info in answer → `400`
- [ ] After 3 counted failures → `409 attempt_limit_exceeded`

---

## 8. Exit criteria

- [ ] Claim create API passes SPEC 15.4 creation tests
- [ ] Mark sub-phase 04 complete in [phase-05/README.md](./README.md)
