# Sub-phase 09 — Enforcement Notifications

**Prerequisites:** [Sub-phases 04–06](./SUBPHASE-08-04-abuse-admin-api.md)

## Summary
**Regression** suite for Section 5.7 — sub-phases 04–06 already fire enforcement notifications inline when resolve/takedown/ban runs (via Phase 03 `INotificationService`). This sub-phase verifies every type and recipient, including Phases 03–07.

## Types introduced in Phase 08 (wired in 04–06)
| Type | Recipient | Fired from |
| ---- | --------- | ---------- |
| `AbuseReportResolvedForFlagger` | Flagger | Sub-phase 04 resolve |
| `AdminTakedownAffectingYou` | Reporter + approved claimant | Sub-phase 05 takedown only |
| `ClaimEndedByEnforcement` | Affected party | Sub-phase 06 ban cleanup only |

## Regression
Automated test: every Section 5.7 row for Phases 03–08 produces exactly one notification per recipient.

## Exit criteria
- [ ] All notification types verified; mark complete in README
