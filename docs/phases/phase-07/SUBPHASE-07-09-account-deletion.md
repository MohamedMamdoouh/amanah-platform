# Sub-phase 09 — Account Deletion

**Prerequisites:** All prior sub-phases 01–08

## Summary
`AccountDeletionService`, `GET /api/account/deletion-status`, `DELETE /api/account`, `AccountDeletionPurge` job (30d PII purge), `/settings/account` UI.

## Deliverables
### Blockers
- Report in `Claim In Progress` (as reporter)
- Approved claim on another's report (as claimant)

### On DELETE
- For each report in `Pending Review` or `Published`: call `ReportLifecycleService.SystemWithdraw` **and** `ClaimCleanupService.ClosePendingClaimsAsync` (closes *other users'* pending claims on those reports per SPEC §5.1)
- Call `ClaimCleanupService.WithdrawUserPendingClaimsAsync` (user's own `Pending` claims as claimant on others' reports)
- Revoke all sessions; sign out immediately
- Anonymize message sender immediately
- Schedule PII purge in 30 days (`AccountDeletionPurge` job respects `RETENTION_DAYS_OVERRIDE`)

### UI
- Settings page with blocker messaging and confirm flow

## Exit criteria
### Phase 07 final gate
- [ ] All [PHASE-07 acceptance criteria](./PHASE-07-lifecycle-retention.md#8-acceptance-criteria) pass
- [ ] Mark sub-phase 09 and Phase 07 complete in README
