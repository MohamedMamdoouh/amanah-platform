# Phase 07 - Lifecycle, Retention & Account Management

**Status:** Not started  
**Prerequisites:** Phase 06 - Chat, Resolution & Notifications

---

## 1. Summary

Implement background scheduled jobs for listing expiry, pending-claim timeout, and data retention cleanup. Enable reporter withdrawal from `Published` reports, account self-deletion with blockers, and the cumulative 90-day published timer (paused during `Claim In Progress`). Introduce shared cleanup services that Phase 08 ban/takedown flows will call.

---

## 2. SPEC references

| SPEC section | Topic |
| ------------ | ----- |
| Section 4.7 | Withdrawal and listing lifecycle (expiry, timer pause) |
| Section 5.1 | Account deletion |
| Section 6.3 | 10-day pending-claim auto-withdraw |
| Section 12 | Data retention (full schedule) |
| Section 15.2 | Expiry acceptance criteria (deferred from Phase 03) |
| Section 15.5 | Chat retention deletion |
| Section 21 | Scheduled jobs |

**Part II (technical):** Section 21 (jobs)

---

## 3. Prerequisites

### Prior phases

- [ ] Phase 01 - Platform Foundation
- [ ] Phase 02 - Report Submission
- [ ] Phase 03 - Admin Moderation
- [ ] Phase 05 - Claims & Verification
- [ ] Phase 06 - Chat, Resolution & Notifications

### Deferred decisions (Section 14)

None additional.

---

## 4. Deliverables

### API

| Method | Route | Purpose |
| ------ | ----- | ------- |
| POST | `/api/v1/reports/{id}/withdraw` | Reporter withdraws `Published` report (extend Phase 02 endpoint) |
| DELETE | `/api/v1/account` | Self-serve account deletion |
| GET | `/api/v1/account/deletion-status` | Check blockers (active approved claims) |

### UI routes

| Route | Access | Purpose |
| ----- | ------ | ------- |
| `/my/reports/{id}` | Logged-in (reporter) | Withdraw button on `Published` reports |
| `/settings/account` | Logged-in | Account deletion with blocker messaging |

### Database

- `Report.publishedSecondsElapsed` - cumulative seconds in `Published` (timer pauses in `Claim In Progress`)
- `Report.publishedTimerResumedAt` - when timer resumed after claim cancellation
- `Report.expiryWarningSent` - boolean for 7-day warning
- Track withdrawal reason (internal enum + optional text)

### Infrastructure - scheduled jobs

All jobs use Africa/Cairo day boundaries where applicable. Run on a configurable schedule (e.g. hourly for timeouts, daily for retention).

| Job | Trigger | Action |
| --- | ------- | ------ |
| `ListingExpiryWarning` | 83 cumulative published days | Send `ReportExpiringSoon` notification |
| `ListingAutoExpiry` | 90 cumulative published days | Withdraw with `_expired_`; close pending claims; notify |
| `PendingClaimTimeout` | 10 days since `submittedAt` | Auto-withdraw claim; notify both parties |
| `RejectedReportCleanup` | 30 days after rejection | Delete report + photos; keep `ModerationAction` |
| `TerminalPhotoCleanup` | On terminal status | Delete report photos immediately |
| `ClaimPhotoCleanup` | On claim terminal status | Delete claim photos |
| `ChatRetention` | 30 days after read-only | Delete thread + messages |
| `OtpCleanup` | 24h after expiry | Delete `OtpCode` rows |
| `OtpSmsOutboxCleanup` | 30 days after `ProcessedAt` | Delete `Sent` and `Failed` rows from `otp_sms_outbox` (limit queries only need recent history) |
| `SessionCleanup` | 30 days after expiry/revoke | Delete `RefreshToken` rows |
| `AccountDeletionPurge` | 30 days after deletion request | Purge direct PII; anonymize sender in messages |

### Shared utilities

- `ReportLifecycleService` - withdraw, expiry, timer pause/resume
- `ClaimCleanupService` - close pending claims on report withdrawal/expiry/takedown
- `RetentionService` - entity-level deletion per Section 12
- `AccountDeletionService` - blockers, cleanup side effects

### Test harness (non-production)

| Config | Purpose |
| ------ | ------- |
| `LISTING_EXPIRY_DAYS` | Override 90-day expiry (e.g. `1` for tests) |
| `LISTING_EXPIRY_WARNING_DAYS_BEFORE` | Override 7-day warning offset (default `7`; warning fires at `LISTING_EXPIRY_DAYS -` this value) |
| `CLAIM_TIMEOUT_MINUTES` | Override 10-day claim timeout (from Phase 05 stub) |
| `RETENTION_DAYS_OVERRIDE` | Override all 30-day retention windows (rejected reports, chat, sessions, account PII purge) |
| `POST /api/v1/admin/test/run-job/{jobName}` | Admin-only manual job trigger for CI (supersedes Phase 05 `trigger-claim-timeout` stub) |

---

## 5. Permissions (Section 9)

Server-enforce these matrix rows before marking this phase done:

| Data | Access on deletion |
| ---- | ------------------ |
| Chat message bodies | Remain until retention deadline; sender anonymized immediately |
| Direct PII (phone, display name) | Purged within 30 days |
| ModerationAction audit | Survives all deletions |

Withdrawal reason: reporter and admin only - enforced in Phase 02; regression in Phase 07 withdraw UI/API tests.

---

## 6. Notifications (Section 5.7)

| Event | Recipient | Introduced |
| ----- | --------- | ---------- |
| Claim auto-withdrawn (reporter timeout) | Reporter and claimant | this phase |
| Report expiring soon | Reporter | this phase |
| Report expired | Reporter and pending claimants | this phase |
| Claim closed - report unavailable | Claimant | this phase (expiry/takedown paths) |

---

## 7. Out of scope

Explicitly deferred to later phases:

- Abuse admin UI and ban/takedown flows -> Phase 08 (will call cleanup services from this phase)
- Claim-ended-by-enforcement notification -> Phase 08
- Admin takedown affecting you -> Phase 08

---

## 8. Acceptance criteria

From [SPEC.md Section 15.2](./SPEC.md#152-moderation-rejection-and-resubmission) (expiry items deferred from Phase 03).

- [ ] **Listing expiry warning:** when a `Published` report has **83 cumulative published days** elapsed (7 days before auto-expiry), the reporter receives `ReportExpiringSoon`
- [ ] **Listing auto-expiry:** after **90 cumulative days** in `Published` (timer paused during `Claim In Progress`), the report becomes `Withdrawn` with `_expired_`; pending claims close; reporter and claimants are notified
- [ ] **No expiry while in review:** `Pending Review` and `Rejected` reports never auto-expire

From [SPEC.md Section 15.4](./SPEC.md#154-claiming-and-review).

- [ ] **Reporter timeout:** a `Pending` claim is auto-withdrawn after **10 days** without reporter action; no attempt is consumed; the report stays `Published`; both parties are notified

From [SPEC.md Section 15.5](./SPEC.md#155-resolution-and-chat).

- [ ] **Chat retention:** after cancellation or resolution the thread is read-only and is deleted 30 days later

From [SPEC.md Section 5.1](./SPEC.md#51-authentication) (account deletion).

- [ ] Account deletion blocked while user has report in `Claim In Progress` or holds approved claim on another's report
- [ ] On deletion: `Pending Review`/`Published` reports withdrawn; pending claims withdrawn; signed out immediately; message bodies remain with anonymized sender; PII purged within 30 days

From [SPEC.md Section 15.2](./SPEC.md#152-moderation-rejection-and-resubmission).

- [ ] **Rejected retention:** a `Rejected` report and its photos are deleted 30 days after rejection when never resubmitted; the moderation decision record survives

**Additional phase gate:**

- [ ] Reporter can withdraw `Published` report (must cancel approved claim first)
- [ ] Withdrawal closes pending claims; optional internal reason recorded
- [ ] Timer pauses in `Claim In Progress`; resumes with remaining time on cancel

---

## 9. Definition of done

### Automated tests

- [ ] Listing expiry warning at 83 days (using override)
- [ ] Auto-expiry at 90 days; pending claims closed
- [ ] Timer pause during `Claim In Progress`; resume on cancel
- [ ] 10-day claim auto-withdraw (using override)
- [ ] Rejected report deleted after 30 days; `ModerationAction` survives
- [ ] Chat deleted 30 days after read-only
- [ ] Account deletion blockers enforced
- [ ] Account deletion cleanup side effects
- [ ] OTP and session cleanup jobs
- [ ] `Pending Review`/`Rejected` never expire

### Manual smoke checklist

- [ ] Withdraw published report with optional reason
- [ ] Trigger expiry job via admin test endpoint; verify notifications
- [ ] Trigger claim timeout job; verify auto-withdraw
- [ ] Delete account; verify blockers when claim in progress
- [ ] Verify read-only chat deleted after retention window (with override)

### Phase exit gate

This phase is complete when all acceptance criteria pass and no out-of-scope items were implemented early.
