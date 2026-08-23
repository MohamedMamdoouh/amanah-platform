# Sub-phase 02 — Report Lifecycle Service

**Prerequisites:** [Sub-phase 01](./SUBPHASE-07-01-lifecycle-schema.md)

## Summary
Implement `ReportLifecycleService`: cumulative published timer (pause during `Claim In Progress`, resume on cancel), reporter/system withdraw, and expiry helpers. Phase 08 ban/takedown calls these methods — do not duplicate withdraw logic in controllers.

## Deliverables
| Method | Purpose |
| ------ | ------- |
| `OnPublished(report)` | Init timer on approval |
| `OnClaimInProgress(report)` | Pause timer; snapshot elapsed |
| `OnClaimCancelled(report)` | Resume; set `publishedTimerResumedAt` |
| `GetCumulativePublishedDays(report)` | For expiry jobs |
| `WithdrawExpired(report)` | System withdraw with `_expired_` reason |
| `ReporterWithdraw(report, reason?)` | Reporter-initiated `Published`/`Pending Review` → `Withdrawn` (HTTP layer in sub-phase 04) |
| `SystemWithdraw(report, reason)` | Callable withdraw for ban/account-deletion cleanup (Phase 08) — not `_expired_`-only |

## Tests
- [ ] Timer does not advance during CIP
- [ ] Resume continues from remaining time
- [ ] `SystemWithdraw` usable without HTTP context

## Exit criteria
- [ ] Unit tests pass; mark complete in README
