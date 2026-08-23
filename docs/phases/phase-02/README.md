# Phase 02 — Sub-phases

This directory contains [PHASE-02-report-submission.md](./PHASE-02-report-submission.md), broken into **10 incremental sub-phases**. Each sub-phase delivers one coherent, testable slice so you can implement, read every line, validate, and test before moving on — no bulk implementation.

**Parent phase:** [PHASE-02 — Report Submission](./PHASE-02-report-submission.md)

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

- Photo uploads: local S3-compatible storage (MinIO or Railway bucket emulator) until sub-phase 04 wires bucket routing
- Use seeded categories/governorates from Phase 01 sub-phase 09

---

## Dependencies

- **01** → **02**, **04**, **07**
- **02**, **03**, **04** → **05**
- **04** → **08**, **09**
- **05** → **06**, **08**
- **07** → **08**, **09**, **10**
- **08** → **09**
- **06**, **08**, **09** → **10**

**Rule:** finish sub-phases 01–06 (all API work) before starting Angular sub-phases 07–10. Within the UI track: **07** (reference) → **08** (lost form) → **09** (found form) → **10** (my reports).

---

## Sub-phase index

| # | Doc | Goal | Status |
| - | --- | ---- | ------ |
| 01 | [SUBPHASE-02-01-reference-data.md](./SUBPHASE-02-01-reference-data.md) | `GET /api/categories` and `GET /api/governorates` | Not started |
| 02 | [SUBPHASE-02-02-validation-utilities.md](./SUBPHASE-02-02-validation-utilities.md) | Contact-info block, field validation, date rules, search-text builder | Not started |
| 03 | [SUBPHASE-02-03-quota-service.md](./SUBPHASE-02-03-quota-service.md) | Daily quota (3/day) and open-report cap (5) | Not started |
| 04 | [SUBPHASE-02-04-photo-upload.md](./SUBPHASE-02-04-photo-upload.md) | Upload, EXIF strip, WebP thumbnail, bucket routing, pre-signed URLs | Not started |
| 05 | [SUBPHASE-02-05-report-create.md](./SUBPHASE-02-05-report-create.md) | `POST /api/reports` with full validation and search column | Not started |
| 06 | [SUBPHASE-02-06-read-withdraw.md](./SUBPHASE-02-06-read-withdraw.md) | `GET /api/reports/mine`, detail, withdraw; permission enforcement | Not started |
| 07 | [SUBPHASE-02-07-angular-reference.md](./SUBPHASE-02-07-angular-reference.md) | Reference-data services, photo upload client, auth guards | Not started |
| 08 | [SUBPHASE-02-08-lost-form.md](./SUBPHASE-02-08-lost-form.md) | `/report/lost` submission form and confirmation screen | Not started |
| 09 | [SUBPHASE-02-09-found-form.md](./SUBPHASE-02-09-found-form.md) | `/report/found` submission form with held-location fields | Not started |
| 10 | [SUBPHASE-02-10-my-reports.md](./SUBPHASE-02-10-my-reports.md) | `/my/reports` Pending Review tab and report detail + withdraw | Not started |

---

## Mapping to Phase 02 deliverables

| Phase 02 deliverable | Sub-phase(s) |
| -------------------- | ------------ |
| `GET /api/categories`, `GET /api/governorates` | 01 |
| Contact-info detector | 02 |
| Field-level validation per category | 02, 05 |
| Date validation (Cairo, 12-month window) | 02, 05 |
| Quota service (3/day, cap 5) | 03, 05 |
| Search text builder → `normalizedSearchText` | 02, 05 |
| `POST /api/uploads/report-photo` + pre-signed URL | 04 |
| EXIF strip, WebP thumbnail, bucket routing | 04 |
| Upload rate limits (5/min, 20/hour) | 04 |
| `POST /api/reports` | 05 |
| `GET /api/reports/mine`, detail, withdraw | 06 |
| Hidden detail never returned to admin | 05, 06 |
| Private photos not in public responses | 04, 06 |
| `/report/lost`, `/report/found` | 08, 09 |
| `/my/reports`, `/my/reports/{id}` | 10 |
| Section 15.1 acceptance criteria | 02–06 (API tests), 08–10 (E2E smoke) |
| Withdrawal reason (reporter + admin) | 06 |
| Section 9 permissions (Phase 02) | 05, 06 |

---

## Phase exit gate

Phase 02 is complete when sub-phase 10 passes and all acceptance criteria in [PHASE-02-report-submission.md](./PHASE-02-report-submission.md#8-acceptance-criteria) are satisfied.
