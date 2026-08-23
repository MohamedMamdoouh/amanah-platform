# Sub-phase 08 — Auth Cleanup & Job Trigger

**Prerequisites:** [Sub-phases 05](./SUBPHASE-07-05-claim-timeout-job.md), [06](./SUBPHASE-07-06-listing-expiry-jobs.md), [07](./SUBPHASE-07-07-retention-jobs.md)

## Summary
`OtpCleanup` (24h post-expiry), `SessionCleanup` (30d post-expiry/revoke). Admin test endpoint `POST /api/admin/test/run-job/{jobName}`.

## Deliverables
- Two cleanup jobs
- Register all Phase 07 jobs in scheduler
- Admin-only manual trigger for CI (non-production or guarded)
- Document job names in README

## Exit criteria
- [ ] OTP/session rows deleted per schedule; job trigger works in test env
- [ ] Mark complete in README
