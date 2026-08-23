# Phase 02 - Report Submission

**Status:** Not started  
**Prerequisites:** Phase 01 - Platform Foundation

---

## 1. Summary

Enable logged-in users to submit lost and found item reports with full field validation, category-specific fields, photo uploads, contact-info blocking, submission quotas, and the hidden verification detail. Reports are created in `Pending Review` status. Reporters can view their submissions in My Reports (Pending Review tab) and withdraw while pending. The normalized search column is populated on every write so Phase 04 browse can read it without a backfill.

---

## 2. SPEC references

| SPEC section | Topic |
| ------------ | ----- |
| Section 4.1 | Reporting a lost item |
| Section 4.2 | Reporting a found item |
| Section 4.1.3 | Contact-info block |
| Section 4.1.6-4.1.7 | Submission quota and concurrent open-report cap |
| Section 4.8 | My Reports (Pending Review tab; Rejected tab read-only until Phase 03) |
| Section 5.2 | Report categories and fields, hidden verification detail, `photosPrivate` |
| Section 5.3 | Location (governorate + area) |
| Section 5.4 | Reward flag |
| Section 7.5 | Photo upload rate limits |
| Section 9 | Reporter permissions, hidden detail, private photos |
| Section 15.1 | Report submission acceptance criteria |
| Section 16 | Search column write (denormalized normalized text) |
| Section 19 | Uploads and media processing |

**Part II (technical):** Section 16 (search column), Section 19

---

## 3. Prerequisites

### Prior phases

- [ ] Phase 01 - Platform Foundation (auth, DB schema, seeds, buckets)

### Deferred decisions (Section 14)

None additional - Phase 01 prerequisites must be complete.

---

## 4. Deliverables

### API

| Method | Route | Purpose |
| ------ | ----- | ------- |
| GET | `/api/categories` | Active categories with field definitions |
| GET | `/api/governorates` | Governorate list |
| POST | `/api/reports` | Submit lost or found report -> `Pending Review` |
| GET | `/api/reports/mine` | Reporter's reports (filterable by status tab) |
| GET | `/api/reports/{id}` | Report detail (reporter + admin only for non-public statuses) |
| POST | `/api/reports/{id}/withdraw` | Reporter withdraws while `Pending Review` |
| POST | `/api/uploads/report-photo` | Upload report photo; EXIF strip, thumbnail, bucket routing |
| GET | `/api/uploads/report-photo/{id}/url` | Pre-signed URL for private photos (reporter/admin only) |

### UI routes

| Route | Access | Purpose |
| ----- | ------ | ------- |
| `/report/lost` | Logged-in | Lost item submission form |
| `/report/found` | Logged-in | Found item submission form |
| `/my/reports` | Logged-in | My Reports - Pending Review tab |
| `/my/reports/{id}` | Logged-in (reporter) | Report detail while pending |

### Database

- No new entities (created in Phase 01); populate `Report`, `CategoryField`, `ReportPhoto`
- `Report.normalizedSearchText` computed and stored on every create/update
- `Report.status` defaults to `Pending Review`
- `Report.resubmissionCount` initialized to 0

### Infrastructure

- Railway Buckets: report photos routed to public or private prefix based on category `photosPrivate`
- WebP thumbnail generation on upload
- Upload rate limiting: 5/min, 20/hour per account (Section 7.5)

### Shared utilities

- Contact-info detector (URL/social domains, 10+ digit phone sequences with Arabic-Indic normalization)
- Field-level validation per category field definitions
- Date validation: not future, not > 12 months ago (Africa/Cairo)
- Quota service: 3 new reports/day, max 5 open reports (`Pending Review`, `Published`, `Claim In Progress`)
- Search text builder: title + description + public category fields + area -> normalized column

---

## 5. Permissions (Section 9)

Server-enforce these matrix rows before marking this phase done:

| Data | Roles granted access |
| ---- | -------------------- |
| Title, description, category fields | Reporter (own), Admin |
| Public photos | Reporter (own), Admin |
| Private photos (`photosPrivate`) | Reporter (own), Admin (review only) |
| Hidden verification detail | Reporter (own) only - **never Admin** |
| Reward amount, item-held location | Reporter (own), Admin |
| Display name of reporter | Reporter (own), Admin |
| Phone numbers | Own user, Admin |
| Withdrawal reason | Reporter (own), Admin - returned on `GET /api/reports/{id}` for `Withdrawn` reports only; never on public/browse APIs |

Non-reporter users and public visitors cannot access `Pending Review` reports (not-found).

---

## 6. Notifications (Section 5.7)

| Event | Recipient | Introduced |
| ----- | --------- | ---------- |
| - | - | Deferred to Phase 03 (admin approval/rejection notifications) |

Submission confirmation is shown inline on the confirmation screen ("usually within a day").

---

## 7. Out of scope

Explicitly deferred to later phases:

- Admin approve/reject -> Phase 03
- Rejected report resubmit -> Phase 03
- Public browse and search -> Phase 04
- Claims -> Phase 05
- Listing expiry and auto-withdraw jobs -> Phase 07
- Reporter withdraw while `Published` -> Phase 07
- In-app notifications -> Phase 03

---

## 8. Acceptance criteria

From [SPEC.md Section 15.1](./SPEC.md#151-report-submission-and-validation).

- [ ] **Valid submission creates a pending report:** given a logged-in user with remaining quota, when they submit all required fields including the hidden verification detail, then a report is created with status `Pending Review`
- [ ] **Date bounds:** a date lost/found in the future, or more than 12 months before today in Africa/Cairo time, is rejected with field-level validation. Today's local date is always accepted
- [ ] **Hidden-detail format:** the hidden verification detail is private text of 10-500 characters, and is required
- [ ] **Contact info is blocked in scoped fields:** URL/social-domain text or a phone-like sequence of 10+ digits after normalization is rejected with field-level validation in title, description, area, held-location detail, public category fields, and claim text - and is accepted in the hidden verification detail and in chat messages
- [ ] **Category fields:** required category fields are validated per the active category's field definitions (Section 5.2), including seed defaults (text 2-80 chars, `first name on document` 2-40 letters/spaces, `key count` integer 1-20)
- [ ] **Submission quota:** at 3 new reports in the current Africa/Cairo day, the next submission is rejected with clear quota messaging
- [ ] **Open-report cap:** at 5 reports in `Pending Review`, `Published`, or `Claim In Progress`, the next new submission is rejected with clear cap messaging; resubmitting a `Rejected` report still succeeds (testable after Phase 03; verify cap logic is implemented now)

**Additional phase gate:**

- [ ] `photosPrivate` category stores photos in private bucket; not returned in public API responses
- [ ] EXIF metadata stripped from uploaded photos
- [ ] Normalized search column populated on create
- [ ] Reporter can withdraw `Pending Review` report
- [ ] No draft saving - single-session submission only

---

## 9. Definition of done

### Automated tests

- [ ] Valid lost and found submissions with all required fields
- [ ] Date validation (future, > 12 months, today accepted)
- [ ] Contact-info block on all scoped fields; hidden detail exempt
- [ ] Category field validation per seed definitions
- [ ] Daily quota (3/day) and open-report cap (5)
- [ ] Hidden detail never returned to admin API
- [ ] Private photos: pre-signed URL only for reporter/admin
- [ ] Upload rate limits (5/min, 20/hour)
- [ ] Search column written correctly with Arabic normalization

### Manual smoke checklist

- [ ] Submit lost report with photos (public category)
- [ ] Submit found report with held-location fields
- [ ] Submit Documents/IDs report - photos not visible in any public-facing response
- [ ] Hit daily quota; clear error message shown
- [ ] Withdraw pending report from My Reports
- [ ] Logged-out user prompted to login on report submission

### Phase exit gate

This phase is complete when all acceptance criteria pass and no out-of-scope items were implemented early.
