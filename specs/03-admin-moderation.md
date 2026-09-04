# Phase 03 - Admin Moderation

**Status:** Not started  
**Prerequisites:** Phase 02 - Report Submission

---

## 1. Summary

Give the admin a FIFO moderation queue to approve or reject pending reports, with predefined rejection reasons and optional notes. Reporters receive in-app notifications on approval/rejection and can fix and resubmit rejected reports (up to 3 times). Admin can manage categories and field definitions. Admin receives email alerts on new submissions. This phase introduces the in-app notification center.

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
| Section 15.2 | Moderation acceptance criteria (except expiry - Phase 07)                       |
| Section 21   | Transactional email for admin alerts                                            |

**Part II (technical):** Section 21 (email)

---

## 3. Prerequisites

### Prior phases

- [ ] Phase 01 - Platform Foundation
- [ ] Phase 02 - Report Submission

### Deferred decisions (Section 14)

Resolve **before starting** this phase:

| Item                         | Notes                                                                                |
| ---------------------------- | ------------------------------------------------------------------------------------ |
| Transactional email provider | Real provider for admin moderation-queue alerts - resolve before starting this phase |

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
| PUT    | `/api/v1/admin/categories/{id}`                  | Edit name, sort order, `photosPrivate`, active flag      |
| POST   | `/api/v1/admin/categories/{id}/fields`           | Add field definition                                     |
| PUT    | `/api/v1/admin/categories/{id}/fields/{fieldId}` | Edit field definition                                    |
| POST   | `/api/v1/reports/{id}/resubmit`                  | Reporter resubmits `Rejected` report -> `Pending Review` |
| PUT    | `/api/v1/reports/{id}`                           | Reporter edits `Rejected` report content                 |
| GET    | `/api/v1/notifications`                          | User notification list                                   |
| GET    | `/api/v1/notifications/unread-count`             | Unread count for header badge                            |
| PATCH  | `/api/v1/notifications/{id}/read`                | Mark notification read                                   |
| POST   | `/api/v1/notifications/read-all`                 | Mark all read                                            |

### UI routes

| Route                    | Access               | Purpose                                                 |
| ------------------------ | -------------------- | ------------------------------------------------------- |
| `/admin/moderation`      | Admin                | Moderation queue (FIFO, pending badge)                  |
| `/admin/moderation/{id}` | Admin                | Review detail with approve/reject                       |
| `/admin/categories`      | Admin                | Category and field management                           |
| `/my/reports`            | Logged-in            | All status tabs incl. Rejected (resubmit) and Published |
| `/my/reports/{id}`       | Logged-in (reporter) | Rejected report with reason, note, edit/resubmit        |
| `/notifications`         | Logged-in            | Notification center                                     |

### Database

- `ModerationAction` records on every approve/reject (survives report deletion)
- `Report.publishedAt` set on approval
- `Report.resubmissionCount` incremented on each resubmit
- `Notification` rows for `ReportApproved`, `ReportRejected`

### Infrastructure

- Transactional email to admin on new report submission
- In-app notification center (source of truth for user events)
- **Cache invalidation:** `ICacheService.RemoveAsync(CacheKeys.Categories)` on every admin category create/update
- **Category translations:** new `code` / `fieldKey` values require matching entries in `web/src/assets/i18n/ar/categories.json` before public deploy (admin UI shows English keys)

### Shared utilities

- Rejection reason enum (8 predefined reasons per Section 5.5)
- Resubmit validation: re-run contact-info block, re-derive photo privacy on category change
- Quota exemption: resubmit does not count against daily submission quota
- Admin moderation search reuses search column from Phase 02, scoped to include non-public statuses

---

## 5. Permissions (Section 9)

Server-enforce these matrix rows before marking this phase done:

| Data                       | Roles granted access                                                        |
| -------------------------- | --------------------------------------------------------------------------- |
| Private photos             | Reporter (own), Admin (review only - enforcement investigation in Phase 08) |
| Hidden verification detail | Reporter (own) only - Admin still **never** sees this                       |
| All public report fields   | Reporter (own), Admin                                                       |
| Withdrawal reason          | Reporter (own), Admin - enforced in Phase 02; regression only in this phase |
| ModerationAction audit     | Admin only (no read API - writes only; vacuously enforced)                  |

`Pending Review` and `Rejected` reports: not-found for everyone except reporter and admin.

---

## 6. Notifications (Section 5.7)

| Event           | Recipient | Introduced |
| --------------- | --------- | ---------- |
| Report approved | Reporter  | this phase |
| Report rejected | Reporter  | this phase |

Admin email (not in-app): new submission waiting in moderation queue.

---

## 7. Out of scope

Explicitly deferred to later phases:

- Public browse and search UI -> Phase 04
- Claims -> Phase 05
- Listing expiry warning and auto-expiry -> Phase 07
- Rejected report 30-day deletion job -> Phase 07
- Admin takedown and ban -> Phase 08
- Abuse report queue -> Phase 08
- Reporter withdraw while `Published` -> Phase 07

---

## 8. Acceptance criteria

From [SPEC.md Section 15.2](./SPEC.md#152-moderation-rejection-and-resubmission).

- [ ] **Approve flow:** approving a `Pending Review` report sets it to `Published` and it appears in public listings (API-level verification; browse UI in Phase 04)
- [ ] **Reject flow:** rejecting sets the status to `Rejected` with the chosen reason and optional note, notifies the reporter, keeps the report and photos, and removes it from the moderation queue. The report is readable by its reporter and the admin, and its URL shows a not-found page to anyone else
- [ ] **Fix and resubmit:** editing and resubmitting a `Rejected` report sets it to `Pending Review`, does not consume the daily submission quota, does not require a free open-report slot, and re-runs the contact-info block
- [ ] **Resubmission cap:** after the 3rd resubmission is rejected, further resubmission of that report is refused with a clear message
- [ ] **Category change on resubmission:** changing a report's category to one with `photosPrivate` makes its existing photos private, and changing to one without makes them public
- [ ] **No editing outside `Rejected`:** content edit attempts are refused while the report is `Pending Review`, `Published`, `Claim In Progress`, or terminal (including reward flag/amount)
- [ ] **Rejected retention:** a `Rejected` report and its photos are deleted 30 days after rejection when never resubmitted; the moderation decision record survives (deletion job in Phase 07; verify `ModerationAction` persists now)

**Deferred within v1:**

- [ ] **Listing expiry warning** -> Phase 07
- [ ] **Listing auto-expiry** -> Phase 07
- [ ] **No expiry while in review** -> Phase 07 (verify `Pending Review`/`Rejected` never expire by design)

---

## 9. Definition of done

### Automated tests

- [ ] Approve: status -> `Published`, `publishedAt` set, notification sent
- [ ] Reject: reason + note stored, notification sent, removed from queue
- [ ] Resubmit: quota not consumed, contact block re-run, max 3 resubmissions
- [ ] Category change re-derives photo privacy
- [ ] Edit refused outside `Rejected`
- [ ] Admin can view private photos; cannot access hidden verification detail
- [ ] `ModerationAction` record created and survives report deletion
- [ ] Admin email sent on new submission
- [ ] Notification center: unread until opened/marked read
- [ ] Admin category write clears `CacheKeys.Categories` (`catalog:categories`)

### Manual smoke checklist

- [ ] Admin sees FIFO queue with pending count badge
- [ ] Approve report; reporter sees `ReportApproved` notification
- [ ] Reject with reason; reporter sees rejection reason in My Reports Rejected tab
- [ ] Reporter edits and resubmits rejected report
- [ ] Admin manages categories at `/admin/categories`
- [ ] Admin moderation search finds pending reports by keyword

### Phase exit gate

This phase is complete when all acceptance criteria pass and no out-of-scope items were implemented early.
