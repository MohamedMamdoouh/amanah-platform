# Sub-phase 01 — Lifecycle Schema & Interfaces

**Prerequisites:** Phase 06 complete

## Summary
Migration adding `publishedSecondsElapsed`, `publishedTimerResumedAt`, `expiryWarningSent` to `Report`. Define interfaces: `IReportLifecycleService`, `IClaimCleanupService`, `IRetentionService`, `IAccountDeletionService`.

## Deliverables
- EF migration for timer fields
- Interface stubs in `api/Services/Lifecycle/`
- Document timer semantics in code comments (pause during CIP, resume on cancel)

## Exit criteria
- [ ] Migration applies; interfaces registered in DI as stubs
- [ ] Mark complete in [phase-07/README.md](./README.md)
