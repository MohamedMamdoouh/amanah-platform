# Sub-phase 08 — Lost Report Form

**Status:** Not started  
**Prerequisites:** [Sub-phase 05 — Report Create API](./SUBPHASE-02-05-report-create.md), [Sub-phase 07 — Angular Reference](./SUBPHASE-02-07-angular-reference.md)

---

## 1. Summary

Build the `/report/lost` submission form: category picker, dynamic fields, location, photos, reward, and hidden verification detail. On success, show a confirmation screen with "usually within a day" messaging. Single-session flow — no draft saving.

---

## 2. SPEC references

| SPEC section | Topic |
| ------------ | ----- |
| Section 4.1 | Lost item reporting flow |
| Section 5.2–5.4 | Fields, reward, hidden detail |
| Section 5.3 | Governorate + area |
| Section 11 | RTL form UX |

---

## 3. What you will learn

- Reactive forms with dynamic `FormArray` / `FormGroup` for category fields
- Client-side length validation mirroring server rules (server remains authoritative)
- Multi-step vs single-page form — v1 is **single page** with sections
- Photo upload orchestration: upload on select, collect IDs for submit
- Confirmation route or inline state after `201` response

**Files to read after implementing:**

- `web/src/app/features/report/lost-report-page/`
- `web/src/app/features/report/report-submission.service.ts`
- `web/src/app/features/report/report-confirmation/`

---

## 4. Deliverables

### Page: `/report/lost`

**Sections (top to bottom, RTL):**

1. Category dropdown (from `CategoriesService`)
2. Title + description (with char counters: 10–80, 20–1,000)
3. Date lost (date picker; max = today Cairo, min = 12 months ago — approximate client hint)
4. Dynamic category fields (`CategoryFieldInputComponent` — re-render on category change)
5. Governorate dropdown + area text (optional, max 120)
6. Photos: up to 5 slots (`PhotoUploadSlotComponent`); show `photosPrivate` warning for Documents/IDs
7. Reward offered checkbox + amount (EGP integer, shown when checked)
8. Hidden verification detail (textarea, 10–500; helper text explaining privacy)
9. Submit button (disabled while uploading or submitting)

### `ReportSubmissionService`

| Method | Purpose |
| ------ | ------- |
| `submitLost(formValue)` | `POST /api/reports` with `type: 'lost'` |

Maps form → API request; surfaces field errors on controls.

### Confirmation screen

After successful submit:

- Message: review is **"usually within a day"** (Arabic copy from SPEC)
- Link to `/my/reports` (Pending Review)
- Link to submit another report
- No draft — navigating away loses in-progress form (acceptable per SPEC)

### Validation UX

- Display server `validation.failed` `errors` on matching form fields
- Quota errors: show full message + retry hint for daily limit
- Network errors: generic retry message

---

## 5. Step-by-step implementation order

1. Create `LostReportPageComponent` with empty reactive form
2. Wire category change → rebuild dynamic field controls
3. Add static fields (title, description, date, location, reward, hidden)
4. Integrate photo upload slots; track `photoIds[]`
5. Implement `ReportSubmissionService.submitLost`
6. Handle API errors → patch form errors
7. Build confirmation view (component or route `/report/lost/confirmation/:id`)
8. Manual E2E: full submit with phones category + 1 photo
9. Remove any dev-only test page from sub-phase 07

---

## 6. Out of scope

- Found report form (sub-phase 09)
- My Reports list (sub-phase 10)
- Contact-info pre-check on client (optional blur validation — skip unless trivial)
- Admin flows

---

## 7. Validation gate

### Automated tests

- [ ] Form invalid when title < 10 chars — submit disabled
- [ ] Category change clears previous category field values
- [ ] `ReportSubmissionService` builds correct request body
- [ ] Field error from API mapped to form control

### Manual smoke checklist

- [ ] Submit complete lost report — lands on confirmation screen
- [ ] DB shows `Pending Review` status
- [ ] Submit with missing hidden detail — server error shown on field
- [ ] Hit daily quota — user-friendly Arabic error message
- [ ] Logged-out user cannot access form (guard from sub-phase 07)

---

## 8. Exit criteria

- [ ] End-to-end lost submission works against local API
- [ ] Confirmation copy matches SPEC
- [ ] Mark sub-phase 08 complete in [phase-02/README.md](./README.md)
