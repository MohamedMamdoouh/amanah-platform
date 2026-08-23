# Sub-phase 07 — Retention Jobs

**Prerequisites:** [Sub-phase 01](./SUBPHASE-07-01-lifecycle-schema.md)

## Summary
`RetentionService` + jobs: `RejectedReportCleanup` (30d), `TerminalPhotoCleanup`, `ClaimPhotoCleanup`, `ChatRetention` (30d after read-only). `RETENTION_DAYS_OVERRIDE` for tests.

## Deliverables
- Delete rejected reports + photos; keep `ModerationAction`
- Delete report photos on terminal status immediately (event hook + job)
- Delete claim photos when claim reaches terminal status **or** when parent report reaches terminal status (SPEC §12 — includes `Approved` claims on `Resolved` reports)
- Delete chat threads + messages 30d after `readOnlyAt`
- Storage bucket object deletion

## Exit criteria
- [ ] Rejected cleanup + chat retention tests pass; mark complete in README
