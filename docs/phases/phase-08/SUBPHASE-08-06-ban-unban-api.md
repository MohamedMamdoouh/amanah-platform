# Sub-phase 06 — Ban & Unban API

**Prerequisites:** Phase 07 `ReportLifecycleService`, `ClaimCleanupService`, Phase 01 auth

## Summary
`GET /api/admin/users` (search), `GET /api/admin/users/{id}` (includes `reportsCount`), `POST ban`, `POST unban`.

## Ban side effects (SPEC 7.2)
Call Phase 07 services — **do not** duplicate withdraw/claim-closure logic:
- Revoke all refresh tokens / sign out everywhere
- `ReportLifecycleService.SystemWithdraw` for user's `Pending Review` + `Published` reports; `ClaimCleanupService.ClosePendingClaimsAsync` on each
- `ClaimCleanupService.WithdrawUserPendingClaimsAsync` for user's own `Pending` claims on **other** reports
- Cancel approved claims user is part of; reporter's CIP report → `Withdrawn`
- Notify counterparties: **`ClaimEndedByEnforcement` only** (not `AdminTakedownAffectingYou` — ban produces `Withdrawn`, not `Removed by Admin`)
- Login refused with ban reason

## Unban
- Clear ban fields; sign-in allowed
- **No** restoration of withdrawn/cancelled content

## Exit criteria
- [ ] Ban/unban full side-effect tests; mark complete in README
