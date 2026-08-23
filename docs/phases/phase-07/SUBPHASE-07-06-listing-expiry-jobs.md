# Sub-phase 06 — Listing Expiry Jobs

**Prerequisites:** [Sub-phase 02](./SUBPHASE-07-02-lifecycle-service.md), [04](./SUBPHASE-07-04-claim-cleanup.md)

## Summary
`ListingExpiryWarning` (warning at `LISTING_EXPIRY_DAYS − LISTING_EXPIRY_WARNING_DAYS_BEFORE`, default 83d) and `ListingAutoExpiry` (90d) jobs. `LISTING_EXPIRY_DAYS` override scales both thresholds. Notifications: `ReportExpiringSoon`, `ReportExpired`.

## Deliverables
- Warning job: set `expiryWarningSent`, notify once
- Expiry job: `Withdrawn` + `_expired_`, close pending claims, notify reporter + claimants
- Skip `Pending Review` / `Rejected`
- Respect timer pause during CIP

## Exit criteria
- [ ] SPEC 15.2 expiry criteria pass with overrides; mark complete in README
