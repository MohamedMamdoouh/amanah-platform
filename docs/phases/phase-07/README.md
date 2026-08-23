# Phase 07 — Sub-phases

This directory contains [PHASE-07-lifecycle-retention.md](./PHASE-07-lifecycle-retention.md), broken into **9 incremental sub-phases**.

**Parent phase:** [PHASE-07 — Lifecycle, Retention & Account Management](./PHASE-07-lifecycle-retention.md)

---

## How to work through sub-phases

1. **Read** the sub-phase doc — jobs use env overrides for local testing
2. **Implement** one service or job at a time
3. **Trigger jobs** via admin test endpoint (sub-phase 08) before waiting on schedules
4. **Checkpoint commit** per sub-phase

**Note:** Phase 08 ban/takedown must call these cleanup services — do not reimplement closure logic there.

---

## Sub-phase index

Work order **#** is the sequence you implement. **File** is the doc filename (differs for rows 03–04: cleanup before withdraw).

| # | File | Doc | Goal | Status |
| - | ---- | --- | ---- | ------ |
| 01 | 07-01 | [SUBPHASE-07-01-lifecycle-schema.md](./SUBPHASE-07-01-lifecycle-schema.md) | Timer fields migration + service interfaces | Not started |
| 02 | 07-02 | [SUBPHASE-07-02-lifecycle-service.md](./SUBPHASE-07-02-lifecycle-service.md) | `ReportLifecycleService` — timer pause/resume, expiry logic | Not started |
| 03 | 07-04 | [SUBPHASE-07-04-claim-cleanup.md](./SUBPHASE-07-04-claim-cleanup.md) | `ClaimCleanupService` — close claims on withdraw/expiry | Not started |
| 04 | 07-03 | [SUBPHASE-07-03-withdraw-published.md](./SUBPHASE-07-03-withdraw-published.md) | Extend withdraw for `Published` + UI | Not started |
| 05 | 07-05 | [SUBPHASE-07-05-claim-timeout-job.md](./SUBPHASE-07-05-claim-timeout-job.md) | `PendingClaimTimeout` job + `ClaimAutoWithdrawn` | Not started |
| 06 | 07-06 | [SUBPHASE-07-06-listing-expiry-jobs.md](./SUBPHASE-07-06-listing-expiry-jobs.md) | Expiry warning + auto-expiry jobs | Not started |
| 07 | 07-07 | [SUBPHASE-07-07-retention-jobs.md](./SUBPHASE-07-07-retention-jobs.md) | `RetentionService` + rejected/photo/chat retention jobs | Not started |
| 08 | 07-08 | [SUBPHASE-07-08-auth-cleanup-jobs.md](./SUBPHASE-07-08-auth-cleanup-jobs.md) | OTP/session cleanup + admin job trigger | Not started |
| 09 | 07-09 | [SUBPHASE-07-09-account-deletion.md](./SUBPHASE-07-09-account-deletion.md) | Account deletion API, `AccountDeletionPurge` job, settings UI | Not started |

---

## Dependencies

- **01** → **02**, **07**
- **02** → **04**, **06**, **09**
- **03** → **04**, **05**, **06**, **09**
- **05**, **06**, **07** → **08**
- **08** → **09**

**Work-order vs file number:** README work-order `#` may differ from filename suffix (e.g. work-order 03 = file `07-04`). Prerequisites in sub-phase docs refer to **file numbers**; this graph uses **work-order** numbers from the index table.

**Rules:** Finish **01–08** before **09**. Work order **03** = claim cleanup file `07-04`; **04** = withdraw published file `07-03`.

---

## Mapping to Phase 07 deliverables

| Deliverable | Sub-phase(s) |
| ----------- | ------------ |
| Timer DB fields | 01 |
| `ReportLifecycleService` | 02, 06 |
| `ClaimCleanupService` | 03, 05, 06 |
| Withdraw `Published` API + UI | 04 |
| `PendingClaimTimeout` job | 05 |
| `ListingExpiryWarning` + `ListingAutoExpiry` | 06 |
| `RetentionService`, `RejectedReportCleanup`, photo cleanup, `ChatRetention` | 07 |
| `OtpCleanup`, `SessionCleanup`, job trigger | 08 |
| `AccountDeletionService`, DELETE account, `AccountDeletionPurge` job, UI | 09 |
| All Phase 07 notifications | 03, 05, 06 |
| Test harness env vars | 05, 06, 07, 08, 09 |

---

## Phase exit gate

Complete when sub-phase 09 passes and [PHASE-07 acceptance criteria](./PHASE-07-lifecycle-retention.md#8-acceptance-criteria) are satisfied.
