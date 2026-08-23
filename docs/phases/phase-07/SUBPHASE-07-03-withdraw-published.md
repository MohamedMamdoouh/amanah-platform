# Sub-phase 03 — Withdraw Published Report

**Prerequisites:** [Sub-phases 02](./SUBPHASE-07-02-lifecycle-service.md), [04](./SUBPHASE-07-04-claim-cleanup.md) (cleanup wired on withdraw)

## Summary
Extend `POST /api/reports/{id}/withdraw` for `Published` status. Must cancel approved claim first (Phase 06). Optional withdrawal reason. UI button on `/my/reports/{id}`.

## Deliverables
- API: `Published` → `Withdrawn`; reject if CIP without cancelled claim
- Call `ClaimCleanupService.ClosePendingClaimsAsync`
- Angular: withdraw dialog with reason picker (reuse Phase 02 enum)

## Exit criteria
- [ ] Withdraw published report E2E; mark complete in README
