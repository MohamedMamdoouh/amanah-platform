# Sub-phase 02 — Claim Validation & Quota

**Status:** Not started  
**Prerequisites:** [Sub-phase 01](./SUBPHASE-05-01-decisions.md), Phase 02 [ContactInfoDetector](../phase-02/SUBPHASE-02-02-validation-utilities.md)

---

## 1. Summary

Implement claim text validation (10–500 chars, contact-info block) and `IClaimQuotaService` for 5 claims/day (Cairo). Service layer only — unit/integration tests, no HTTP yet.

---

## 2. SPEC references

| SPEC section | Topic |
| ------------ | ----- |
| Section 6.2 | Claim text, photo, daily quota |
| Section 4.1.3 | Contact-info block on claim text |
| Section 15.4 | Claim creation constraints |

---

## 3. What you will learn

- Reusing `ContactInfoDetector` on claim answer text
- Direction-specific prompt copy keys (UI in sub-phase 08; report direction comes from the public detail API — not validated on claim create)
- Cairo day boundary for claim quota (same pattern as report quota Phase 02)

**Files to read after implementing:**

- `api/Utilities/ClaimTextValidator.cs`
- `api/Services/Claims/IClaimQuotaService.cs`
- `api/Services/Claims/ClaimQuotaService.cs`
- `api/Services/Claims/IClaimAttemptService.cs` — check 3-attempt limit

---

## 4. Deliverables

### `ClaimTextValidator`

| Rule | Detail |
| ---- | ------ |
| Length | 10–500 after `TextNormalizer` |
| Contact-info | `ContactInfoDetector` — hard block |
| Required | Non-empty body |

### `IClaimQuotaService`

- 5 new claim submissions per Cairo day per user
- Error: `claim.quota_daily_exceeded` + `Retry-After`

### `IClaimAttemptService`

| Method | Purpose |
| ------ | ------- |
| `GetAttemptCount(userId, reportId)` | Count claims where `countsAsAttempt = true` |
| `CanSubmit(userId, reportId)` | `false` if ≥ 3 counted attempts |

Counted attempts = claims with `countsAsAttempt = true` for this user+report.

---

## 5. Step-by-step implementation order

1. Implement `ClaimTextValidator` + unit tests
2. Implement `ClaimQuotaService` (mirror `SubmissionQuotaService`)
3. Implement `ClaimAttemptService`
4. Register in DI
5. Integration tests with seeded claims

---

## 6. Out of scope

- HTTP endpoints (sub-phase 04)
- Claim photo (sub-phase 03)

---

## 7. Validation gate

- [ ] Contact-info in claim text → validation error
- [ ] 9 chars → too short; 501 → too long
- [ ] 6th claim same day → quota blocked
- [ ] 3 counted attempts → `CanSubmit` false
- [ ] Withdrawn claim → no attempt counted

---

## 8. Exit criteria

- [ ] All validator/quota tests pass
- [ ] Mark sub-phase 02 complete in [phase-05/README.md](./README.md)
