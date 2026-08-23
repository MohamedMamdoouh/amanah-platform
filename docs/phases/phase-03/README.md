# Phase 03 — Sub-phases

This directory contains [PHASE-03-admin-moderation.md](./PHASE-03-admin-moderation.md), broken into **11 incremental sub-phases**. Each sub-phase delivers one coherent, testable slice so you can implement, read every line, validate, and test before moving on — no bulk implementation.

**Parent phase:** [PHASE-03 — Admin Moderation](./PHASE-03-admin-moderation.md)

---

## How to work through sub-phases

For each sub-phase, follow this loop — do not skip steps:

1. **Read** the sub-phase doc and its linked SPEC sections
2. **Implement** only what the doc lists (resist adding extras)
3. **Read your diff** line by line; annotate anything unclear
4. **Run gate tests** listed in the doc
5. **Manual check** the smoke items
6. **Checkpoint commit** (one commit per sub-phase keeps history readable)
7. Mark the sub-phase complete in the progress table below before starting the next

**Local development defaults:**

- Email: `ConsoleEmailSender` logs admin alerts to the console (sub-phases 01–10)
- Real email provider: wired in sub-phase 11 or alongside production deploy
- In-app notifications: fully local — no external service

---

## Dependencies

- **01** → **02**, **05**
- **02**, **03** → **04**
- **03** → **06**, **09**
- **04** → **07**, **09**, **10**
- **04**, **05**, **06** → **09**
- **02**, **04**, **07** → **10**
- **08** → **11**
- **09** → **11**

**Rules:**

- Finish sub-phases **01–08** (all API work) before starting Angular **09–11**
- Sub-phase 05 (admin email) can run in parallel with 03–04 after 01, but must complete before 09
- Sub-phase 07 (resubmit) requires sub-phase 04 (rejection fields on `Report`) and Phase 02 validators

---

## Sub-phase index

| # | Doc | Goal | Status |
| - | --- | ---- | ------ |
| 01 | [SUBPHASE-03-01-decisions.md](./SUBPHASE-03-01-decisions.md) | Email provider decision; rejection reason enum; moderation/notification types | Not started |
| 02 | [SUBPHASE-03-02-notifications-api.md](./SUBPHASE-03-02-notifications-api.md) | `INotificationService` + notification center API | Not started |
| 03 | [SUBPHASE-03-03-moderation-queue.md](./SUBPHASE-03-03-moderation-queue.md) | FIFO queue + admin report detail (incl. private photos) | Not started |
| 04 | [SUBPHASE-03-04-approve-reject.md](./SUBPHASE-03-04-approve-reject.md) | Approve/reject actions, `ModerationAction` audit, in-app notifications | Not started |
| 05 | [SUBPHASE-03-05-admin-email.md](./SUBPHASE-03-05-admin-email.md) | Admin email alert on new report submission | Not started |
| 06 | [SUBPHASE-03-06-moderation-search.md](./SUBPHASE-03-06-moderation-search.md) | Admin keyword search over Pending/Rejected reports | Not started |
| 07 | [SUBPHASE-03-07-resubmit-api.md](./SUBPHASE-03-07-resubmit-api.md) | Reporter edit + resubmit rejected reports | Not started |
| 08 | [SUBPHASE-03-08-category-admin-api.md](./SUBPHASE-03-08-category-admin-api.md) | Admin category and field-definition CRUD | Not started |
| 09 | [SUBPHASE-03-09-admin-moderation-ui.md](./SUBPHASE-03-09-admin-moderation-ui.md) | `/admin/moderation` queue and review detail | Not started |
| 10 | [SUBPHASE-03-10-notifications-resubmit-ui.md](./SUBPHASE-03-10-notifications-resubmit-ui.md) | Notification center + My Reports Rejected/Published + resubmit | Not started |
| 11 | [SUBPHASE-03-11-category-admin-ui.md](./SUBPHASE-03-11-category-admin-ui.md) | `/admin/categories` management UI | Not started |

---

## Mapping to Phase 03 deliverables

| Phase 03 deliverable | Sub-phase(s) |
| -------------------- | ------------ |
| Rejection reason enum (8 reasons) | 01, 04 |
| `ModerationAction` audit records | 03, 04 |
| `GET /api/admin/moderation/queue` | 03 |
| `GET /api/admin/moderation/reports/{id}` | 03 |
| Approve → `Published`, `publishedAt` | 04 |
| Reject → `Rejected` + reason + note | 04 |
| `ReportApproved` / `ReportRejected` notifications | 02, 04 |
| Admin email on new submission | 01, 05 |
| `GET /api/admin/moderation/search` | 06 |
| `PUT /api/reports/{id}` + `POST resubmit` | 07 |
| Quota exemption + resubmission cap (3) | 07 |
| Photo privacy re-derive on category change | 07 |
| Admin category/field management API | 08 |
| Notification center API | 02 |
| `/admin/moderation`, `/admin/moderation/{id}` | 09 |
| `/notifications`, My Reports Rejected/Published | 10 |
| `/admin/categories` | 11 |
| Hidden detail never shown to admin | 03, 09 |
| Open-report cap exempt on resubmit | 07 |
| `GET /api/notifications/unread-count` | 02 |
| Section 15.2 acceptance criteria | 04, 07 (API), 09–10 (E2E) |
| Section 9 permissions (Phase 03) | 03, 04, 09 |

---

## Phase exit gate

Phase 03 is complete when sub-phase 11 passes and all acceptance criteria in [PHASE-03-admin-moderation.md](./PHASE-03-admin-moderation.md#8-acceptance-criteria) are satisfied.
