# Sub-phase 09 — Found Report Form

**Status:** Not started  
**Prerequisites:** [Sub-phase 08 — Lost Report Form](./SUBPHASE-02-08-lost-form.md)

---

## 1. Summary

Build `/report/found` — same foundation as the lost form plus found-specific fields: date found and item held location (dropdown + conditional detail). Reuse shared components and `ReportSubmissionService` where possible.

---

## 2. SPEC references

| SPEC section | Topic |
| ------------ | ----- |
| Section 4.2 | Found item reporting |
| Section 4.1 | Shared fields (photos, contact block, quota, etc.) |

---

## 3. What you will learn

- Extracting shared form logic into a base component or composable service (without over-abstracting)
- Conditional validators: detail required when held location is `Other`
- Enum dropdown for held location with Arabic labels

**Files to read after implementing:**

- `web/src/app/features/report/found-report-page/`
- `web/src/app/features/report/shared/` (if extracted from sub-phase 08)
- `web/src/app/features/report/report-submission.service.ts` (`submitFound`)

---

## 4. Deliverables

### Page: `/report/found`

Same sections as lost form (sub-phase 08), with these differences:

| Field | Lost | Found |
| ----- | ---- | ----- |
| Date label | Date lost | Date found |
| Held location | — | Required dropdown |
| Held location detail | — | Required when `Other`; max 120 chars |

### Held location enum (Arabic labels)

| Value | Label (example Arabic) |
| ----- | ---------------------- |
| `WithFinder` | مع الشخص الذي وجدها |
| `PoliceStation` | قسم شرطة |
| `BuildingSecurity` | أمن المبنى |
| `Workplace` | مكان العمل |
| `Other` | أخرى |

### `ReportSubmissionService.submitFound`

`POST /api/reports` with `type: 'found'`, `itemHeldLocation`, `itemHeldLocationDetail`.

### Shared extraction (recommended)

If sub-phase 08 duplicated logic, extract now:

- `ReportFormBaseComponent` or shared template for common sections
- Keep found-only fields in `FoundReportPageComponent`

Do not refactor beyond what sub-phases 08–09 need.

### Confirmation

Reuse confirmation component from sub-phase 08.

---

## 5. Step-by-step implementation order

1. Copy/adapt `LostReportPageComponent` → `FoundReportPageComponent`
2. Add held location `FormControl` + detail with conditional validator
3. Change date label and API `type` to `found`
4. Add `submitFound` to service
5. Wire route `/report/found` (guard already from sub-phase 07)
6. Extract shared pieces only if duplication is painful
7. Manual E2E: found report with `PoliceStation` (no detail) and with `Other` + detail

---

## 6. Out of scope

- Public listing display of held location (Phase 04)
- My Reports UI (sub-phase 10)
- Further form refactoring for Phase 03 resubmit

---

## 7. Validation gate

### Automated tests

- [ ] Detail control required when location is `Other`
- [ ] Detail not required for `WithFinder`
- [ ] `submitFound` sends correct `itemHeldLocation` enum value

### Manual smoke checklist

- [ ] Submit found report with held location = police — success
- [ ] Submit with `Other` and empty detail — server/client validation error
- [ ] Found report appears in API `GET /api/reports/mine?status=pending-review`
- [ ] Photos + Documents/IDs category — private upload warning still shown

---

## 8. Exit criteria

- [ ] Both `/report/lost` and `/report/found` work end-to-end
- [ ] Mark sub-phase 09 complete in [phase-02/README.md](./README.md)
