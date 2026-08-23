# Sub-phase 05 — Takedown API

**Prerequisites:** Phase 07 `ReportLifecycleService`, `ClaimCleanupService`, [Sub-phase 04](./SUBPHASE-08-04-abuse-admin-api.md)

## Summary
`POST /api/admin/reports/{id}/takedown` — report → `Removed by Admin`.

## Rules (SPEC 15.6)
- If `Claim In Progress`: cancel approved claim first, chat read-only
- Close pending claims via `ClaimCleanupService`
- Notify reporter + approved claimant: `AdminTakedownAffectingYou` (inline via `INotificationService`)
- Do **not** duplicate cleanup logic — call Phase 07 services

## Exit criteria
- [ ] Takedown during CIP tested; mark complete in README
