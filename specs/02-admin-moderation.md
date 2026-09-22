# Phase 02 - Admin Moderation

**Status:** Complete (API + Angular UI + automated tests). Manual smoke checklist (#9) not yet run in staging.  
**Prerequisites:** Phase 01 - Report Submission

---

## 1. Summary

Give the admin a FIFO moderation queue to approve or reject pending reports, with predefined rejection reasons and optional notes. Reporters receive in-app notifications on approval/rejection and can fix and resubmit rejected reports (up to 3 times). Admin can manage categories and field definitions. Admin receives transactional email alerts on new submissions (Resend via outbox). This phase introduces the in-app notification center.

---

## 2. SPEC references

| SPEC section | Topic                                                                           |
| ------------ | ------------------------------------------------------------------------------- |
| Section 4.3  | Admin review, rejection, and resubmission                                       |
| Section 4.8  | My Reports - Published and Rejected tabs                                        |
| Section 5.5  | Admin moderation, rejection reasons, category management                        |
| Section 5.7  | Admin email on new submission; `ReportApproved`, `ReportRejected` notifications |
| Section 8    | Status transitions through `Published` / `Rejected`                             |
| Section 9    | Admin private-photo access during review                                        |
| Section 12   | `ModerationAction` audit persistence                                            |
| Section 15.2 | Moderation acceptance criteria (expiry completed in Phase 06)                   |
| Section 21   | Transactional email for admin alerts                                            |

**Part II (technical):** Section 21 (email)

---

## 3. Prerequisites

### Prior phases

- [x] Phase 00 - Platform foundation
- [x] Phase 01 - Report Submission

### Deferred decisions (Section 14)

| Item                         | Status                                                                               |
| ---------------------------- | ------------------------------------------------------------------------------------ |
| Transactional email provider | **Done** — [Resend](https://resend.com/) via outbox + `ResendAdminAlertEmailSender` ([deployment.md](../docs/deployment.md)) |

---

## 4. Deliverables

### API

| Method | Route                                            | Purpose                                                  |
| ------ | ------------------------------------------------ | -------------------------------------------------------- |
| GET    | `/api/v1/admin/moderation/queue`                 | FIFO pending reports with pending-count                  |
| GET    | `/api/v1/admin/moderation/reports/{id}`          | Full report for review (incl. private photos)            |
| POST   | `/api/v1/admin/moderation/reports/{id}/approve`  | Approve -> `Published`                                   |
| POST   | `/api/v1/admin/moderation/reports/{id}/reject`   | Reject with reason + optional note -> `Rejected`         |
| GET    | `/api/v1/admin/moderation/search`                | Keyword search incl. Pending/Rejected                    |
| GET    | `/api/v1/admin/categories`                       | List all categories (incl. inactive)                     |
| POST   | `/api/v1/admin/categories`                       | Add category                                             |
| PUT    | `/api/v1/admin/categories/{id}`                  | Edit `code`, sort order, `photosPrivate`, `isActive` (409 if `photosPrivate` changes when reports exist) |
| POST   | `/api/v1/admin/categories/{id}/fields`           | Add field definition                                     |
| PUT    | `/api/v1/admin/categories/{id}/fields/{fieldId}` | Edit field definition                                    |
| POST   | `/api/v1/reports/{id}/resubmit`                  | Reporter resubmits `Rejected` report -> `Pending Review` |
| PUT    | `/api/v1/reports/{id}`                           | Reporter edits `Rejected` report (`multipart/form-data`, same as create); returns **204** |
| GET    | `/api/v1/notifications`                          | User notification list                                   |
| GET    | `/api/v1/notifications/unread-count`             | Unread count for header badge                            |
| PATCH  | `/api/v1/notifications/{id}/read`                | Mark notification read                                   |
| POST   | `/api/v1/notifications/read-all`                 | Mark all read                                            |

**HTTP status summary:** queue/search/categories GET → **200**; approve/reject/resubmit/withdraw/update/mark-read → **204**.

**Rejection reason codes:** `rejection.unclear_photos`, `rejection.spam_or_scam`, `rejection.duplicate`, `rejection.insufficient_description`, `rejection.contact_info`, `rejection.prohibited_item`, `rejection.wrong_category`, `rejection.raw_id_number` (i18n in `rejection-reasons.json`).

### UI routes

| Route                    | Access               | Purpose                                                 |
| ------------------------ | -------------------- | ------------------------------------------------------- |
| `/admin/moderation`      | Admin                | Moderation queue (FIFO, pending badge, keyword search)  |
| `/admin/moderation/{id}` | Admin                | Review detail with approve/reject                       |
| `/admin/categories`      | Admin                | Category and field management                           |
| `/my/reports`            | Logged-in            | Tabs: Pending Review, Rejected, Published               |
| `/my/reports/{id}`       | Logged-in (reporter) | Rejected report with reason, note, edit/resubmit        |
| `/notifications`         | Logged-in            | Notification center                                     |

### Database

- `ModerationAction` records on every approve/reject (survives report deletion)
- `Report.publishedAt` set on approval
- `Report.resubmissionCount` incremented on each resubmit
- `Notification` rows for `ReportApproved`, `ReportRejected`
- `admin_alert_email_outbox` — transactional outbox for admin moderation alerts (same commit as report submit/resubmit)

### Infrastructure

- **Resend** transactional email to admin on new report submission (outbox pattern: `AdminSubmissionAlertNotifier` enqueues; `AdminAlertEmailOutboxProcessor` dispatches)
- Branded HTML + plain-text templates (`AdminAlertEmailTemplates`)
- Status-aware retry: transient Resend errors (429, 5xx) stay `Pending`; permanent 4xx → `Failed`
- In-app notification center (source of truth for user events)
- **Cache invalidation:** `ICacheService.RemoveAsync(CacheKeys.Categories)` on every admin category create/update
- **Category translations:** new `code` / `fieldKey` values require matching entries in `web/src/assets/i18n/ar/categories.json` before public deploy (admin UI shows English keys)

### Shared utilities

- Rejection reason enum (8 predefined reasons per Section 5.5)
- Resubmit validation: re-run contact-info block, re-derive photo privacy on category change
- Quota exemption: resubmit does not count against daily submission quota; open-cap exempt on resubmit
- Admin moderation search reuses search column from Phase 01, scoped to include non-public statuses

---

## 5. Permissions (Section 9)

These rows are server-enforced:

| Data                       | Roles granted access                                                        |
| -------------------------- | --------------------------------------------------------------------------- |
| Private photos             | Reporter (own), Admin during review, and Admin during an open flagged-listing investigation |
| Hidden verification detail | Reporter (own) only - Admin still **never** sees this                       |
| All public report fields   | Reporter (own), Admin                                                       |
| Withdrawal reason          | Reporter (own), Admin - enforced in Phase 01; regression only in this phase |
| ModerationAction audit     | Admin only (no read API - writes only; vacuously enforced)                  |

`Pending Review` and `Rejected` reports: not-found for everyone except reporter and admin.

---

## 6. Notifications (Section 5.7)

| Event           | Recipient | Introduced |
| --------------- | --------- | ---------- |
| Report approved | Reporter  | this phase |
| Report rejected | Reporter  | this phase |

Admin email (not in-app): new submission waiting in moderation queue (Resend outbox).

---

## 7. Out of scope

Shipped in later phases:

- Public browse (Phase 03)
- Claims and the My Reports `claim_in_progress` tab (Phase 04)
- Listing expiry, rejected-report deletion, and reporter withdraw while `Published` (Phase 06)
- Admin takedown, ban, and the abuse queue (Phase 07)

---

## 8. Acceptance criteria

From [SPEC.md Section 15.2](./SPEC.md#152-moderation-rejection-and-resubmission).

- [x] **Approve flow:** approving a `Pending Review` report sets it to `Published` and it appears in public listings (API-level verification; browse UI in Phase 03)
- [x] **Reject flow:** rejecting sets the status to `Rejected` with the chosen reason and optional note, notifies the reporter, keeps the report and photos, and removes it from the moderation queue. The report is readable by its reporter and the admin, and its URL shows a not-found page to anyone else
- [x] **Fix and resubmit:** editing and resubmitting a `Rejected` report sets it to `Pending Review`, does not consume the daily submission quota, does not require a free open-report slot, and re-runs the contact-info block
- [x] **Resubmission cap:** after the 3rd resubmission is rejected, further resubmission of that report is refused with a clear message
- [x] **Category change on resubmission:** changing a report's category to one with `photosPrivate` makes its existing photos private, and changing to one without makes them public
- [x] **No editing outside `Rejected`:** content edit attempts are refused while the report is `Pending Review`, `Published`, `Claim In Progress`, or terminal (including reward flag/amount)
- [x] **Rejected retention:** `ModerationAction` persists; 30-day report and photo deletion shipped in Phase 06 (`RejectedReportCleanup`)

**Shipped in Phase 06** (acceptance checked in [06-lifecycle-retention.md](./06-lifecycle-retention.md) #8): listing expiry warning, listing auto-expiry, and no expiry while a report is in review.

---

## 9. Definition of done

### Automated tests

- [x] Approve: status -> `Published`, `publishedAt` set, notification sent (`ModerationFlowTests`, `NotificationTests`)
- [x] Reject: reason + note stored, notification sent, removed from queue
- [x] Resubmit: quota not consumed, open-cap exempt, contact block re-run, max 3 resubmissions (`ReportResubmitTests`)
- [x] Category change re-derives photo privacy
- [x] Edit refused outside `Rejected`
- [x] Admin can view private photos; cannot access hidden verification detail
- [x] `ModerationAction` record created and survives report deletion
- [x] Admin email outbox: enqueue on submit/resubmit, dispatch, transient vs permanent failure (`ReportAdminAlertEmailTests`)
- [x] Notification center: unread until opened/marked read
- [x] Admin category write clears `CacheKeys.Categories` (`CategoryAdminTests`)
- [x] Admin moderation search: pending/rejected only, Arabic normalization (`ModerationFlowTests`)

### Manual smoke checklist

- [ ] Admin sees FIFO queue with pending count badge
- [ ] Approve report; reporter sees `ReportApproved` notification
- [ ] Reject with reason; reporter sees rejection reason in My Reports Rejected tab
- [ ] Reporter edits and resubmits rejected report
- [ ] Admin manages categories at `/admin/categories` (UI implemented; verify in staging)
- [ ] Admin moderation search finds pending reports by keyword
- [ ] Staging: Resend admin alert email received on submit and resubmit

### Phase exit gate

Automated criteria and deliverables are complete. Run manual smoke (especially Resend in staging) before treating Phase 02 as production-ready. Update this doc when manual smoke passes.
