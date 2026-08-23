# Sub-phase 05 — Pending Claim Timeout Job

**Prerequisites:** [Sub-phase 04](./SUBPHASE-07-04-claim-cleanup.md)

## Summary
`PendingClaimTimeout` scheduled job using `CLAIM_TIMEOUT_MINUTES` (default 14400). Calls `ClaimCleanupService.AutoWithdrawStalePendingClaimsAsync()` — **does not** send notifications itself (service owns notify logic).

## Deliverables
- `PendingClaimTimeoutJob` (IHostedService or Hangfire/Quartz — match project choice)
- Wire `ClaimTimeoutOptions` from Phase 05 stub
- Prefer `POST /api/admin/test/run-job/PendingClaimTimeout` (sub-phase 08) over the Phase 05 `trigger-claim-timeout` stub
- Integration test with overridden minutes

## Exit criteria
- [ ] 10-day (overridden) timeout test passes; mark complete in README
