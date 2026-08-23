# Sub-phase 09 — Claim Cleanup Service & Timeout Stub

**Status:** Not started  
**Prerequisites:** [Sub-phase 06](./SUBPHASE-05-06-withdraw-read.md)

---

## 1. Summary

Introduce reusable `ClaimCleanupService.ClosePendingClaimsAsync` for closing `Pending` claims when a report leaves `Published` (SPEC 6.7). Unit-test the service directly in this sub-phase. **End-to-end** closure on reporter withdraw of a `Published` report ships in Phase 07 (that withdraw path does not exist until then). Optionally hook the Phase 02 `Pending Review` withdraw endpoint as a defensive no-op (claims exist only on `Published` reports per SPEC 6.1). Add `CLAIM_TIMEOUT_MINUTES` env var and optional admin test trigger stub for Phase 07.

---

## 2. SPEC references

| SPEC section | Topic |
| ------------ | ----- |
| Section 6.7 | Pending claim closure when report leaves `Published` |
| Section 15.4 | Pending claim closure (full E2E deferred to Phase 07) |
| Phase 05 parent | 10-day timeout test stub |

---

## 3. What you will learn

- Building cleanup logic Phase 07/08 will call (expiry, takedown, ban)
- Why `Pending Review` withdraw cannot exercise pending-claim closure (claims only on `Published`)
- Env-based timeout override for the future job

**Files to read after implementing:**

- `api/Services/Claims/ClaimCleanupService.cs`
- `api/Configuration/ClaimTimeoutOptions.cs`

---

## 4. Deliverables

### `ClaimCleanupService.ClosePendingClaimsAsync(reportId, reason)`

Reusable method for Phase 07/08:

1. Find all `Pending` claims on the report
2. Each → closed with reason `ReportUnavailable`, `countsAsAttempt = false`
3. Notify each claimant: `ClaimClosedReportUnavailable`

### Optional hook: Phase 02 withdraw (`Pending Review`)

If wired into the existing withdraw handler, this path is normally a no-op (no `Pending` claims on non-`Published` reports). Keeps one code path for all withdraw events; do not treat this as the Phase 05 acceptance test for closure.

### Phase 07 integration (document, do not implement here)

When a reporter withdraws a `Published` report, Phase 07 calls `ClosePendingClaimsAsync` after canceling any approved claim (SPEC 4.7).

### Timeout stub (no job yet)

```csharp
// ClaimTimeoutOptions from env CLAIM_TIMEOUT_MINUTES (default 14400)
// Used by Phase 07 PendingClaimTimeout job
```

Optional: `POST /api/admin/test/trigger-claim-timeout` — returns `501 Not Implemented` with comment, or no-op stub for CI hook documentation. Phase 07's generic `POST /api/admin/test/run-job/{jobName}` supersedes this for integration tests; keep this endpoint documented as an optional early hook only.

---

## 5. Step-by-step implementation order

1. Create `ClaimCleanupService` with `ClosePendingClaimsAsync`
2. Unit/integration test: seed `Published` report with `Pending` claims → call service directly → claimants notified, no attempt consumed
3. (Optional) Call service from Phase 02 withdraw handler
4. Add `ClaimTimeoutOptions` configuration class
5. Document env var in README or `.env.example`

---

## 6. Out of scope

- Actual 10-day timeout job (Phase 07)
- Withdraw `Published` report (Phase 07) — **this is where pending-claim closure is E2E-tested**
- Expiry/takedown/ban closure triggers (Phase 07/08)

---

## 7. Validation gate

- [ ] `ClosePendingClaimsAsync` closes pending claims; no attempt consumed
- [ ] `ClaimClosedReportUnavailable` notification sent
- [ ] `CLAIM_TIMEOUT_MINUTES` read from config
- [ ] Service callable by Phase 07 withdraw/expiry jobs (interface documented)

### Phase 05 final gate

Re-run [PHASE-05 definition of done](./PHASE-05-claims-verification.md#9-definition-of-done).

---

## 8. Exit criteria

- [ ] All Phase 05 acceptance criteria pass
- [ ] Mark sub-phase 09 and Phase 05 complete in [phase-05/README.md](./README.md)
