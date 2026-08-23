# Sub-phase 04 — Claim Cleanup Service

**Prerequisites:** Phase 05 [Sub-phase 09](../phase-05/SUBPHASE-05-09-withdraw-closure.md) (initial stub)

## Summary
Full `ClaimCleanupService`: close pending claims on withdraw/expiry/takedown/ban (Phase 08 calls the same methods). **Notifications** for auto-withdraw and closure are sent from this service — timeout job (sub-phase 05) only invokes `AutoWithdrawStalePendingClaimsAsync()`.

## Deliverables
| Method | Purpose |
| ------ | ------- |
| `ClosePendingClaimsAsync(reportId, reason)` | Close pending claims; notify `ClaimClosedReportUnavailable` |
| `AutoWithdrawStalePendingClaimsAsync()` | Auto-withdraw stale `Pending` claims; notify `ClaimAutoWithdrawn` |
| `WithdrawUserPendingClaimsAsync(userId)` | Withdraw user's own `Pending` claims as claimant (ban/account deletion) |

## Exit criteria
- [ ] Pending claims closed on withdraw; no attempt consumed
- [ ] Mark complete in README
