# Sub-phase 10 — My Reports UI

**Status:** Not started  
**Prerequisites:** [Sub-phase 06 — Read & Withdraw API](./SUBPHASE-02-06-read-withdraw.md), [Sub-phases 08–09](./SUBPHASE-02-08-lost-form.md) (forms working)

---

## 1. Summary

Build `/my/reports` with the **Pending Review** tab and `/my/reports/{id}` detail view. Reporter can read full detail (including hidden verification detail) and withdraw pending reports. Other status tabs are placeholders until later phases.

---

## 2. SPEC references

| SPEC section | Topic |
| ------------ | ----- |
| Section 4.8 | My Reports tabs |
| Section 4.7 | Withdraw while Pending Review |
| Section 9 | Reporter-only hidden detail |

---

## 3. What you will learn

- Tab navigation with only one active tab implemented (others disabled or "coming soon")
- Detail page fetching with reporter-scoped API
- Withdraw confirmation dialog and optimistic UI update
- Displaying private photos via URL refresh on expiry (SPEC 16)

**Files to read after implementing:**

- `web/src/app/features/my-reports/my-reports-page/`
- `web/src/app/features/my-reports/report-detail-page/`
- `web/src/app/features/my-reports/my-reports.service.ts`

---

## 4. Deliverables

### Page: `/my/reports`

**Tabs (Phase 02 scope in bold):**

| Tab | Phase | Behavior |
| --- | ----- | -------- |
| **Pending Review** | **02** | List from `GET /api/reports/mine?status=pending-review` |
| Rejected | 03 | Disabled or empty state: "متاح قريباً" |
| Published | 04+ | Disabled |
| Claim In Progress | 05+ | Disabled |
| Closed | 07+ | Disabled |

**List item:** title, category, type (lost/found), created date, thumbnail (if public photo).

Click row → `/my/reports/{id}`.

Empty state: link to `/report/lost` and `/report/found`.

### Page: `/my/reports/{id}`

- Full report detail for reporter
- Show hidden verification detail in a clearly labeled private section
- Photo gallery (refresh presigned URLs on image error)
- **Withdraw** button (only while `Pending Review`)
- Withdraw dialog: optional reason picker (matches API enum)
- After withdraw → navigate back to list or show withdrawn state

### `MyReportsService`

| Method | API |
| ------ | --- |
| `getPendingReports()` | `GET /api/reports/mine?status=pending-review` |
| `getReport(id)` | `GET /api/reports/{id}` |
| `withdraw(id, reason?)` | `POST /api/reports/{id}/withdraw` |

---

## 5. Step-by-step implementation order

1. Implement `MyReportsService`
2. Build list page with Pending Review tab only
3. Build detail page with all reporter-visible fields
4. Add withdraw flow with confirmation modal
5. Handle `404` on detail — report withdrawn or not found
6. Link from confirmation screen (sub-phase 08) to this list
7. Full manual smoke of Phase 02 acceptance criteria

---

## 6. Out of scope

- Rejected tab resubmit UI (Phase 03)
- Claims section (Phase 05)
- Public report URLs (Phase 04)
- In-app notifications badge (Phase 03)

---

## 7. Validation gate

### Automated tests

- [ ] List component renders items from service mock
- [ ] Detail shows hidden verification section for reporter
- [ ] Withdraw calls API and navigates on success

### Manual smoke checklist (Phase 02 exit)

- [ ] Submit lost report with photos (public category) — visible in Pending Review
- [ ] Submit found report with held-location fields — detail page correct
- [ ] Submit Documents/IDs — photos visible to reporter on detail only
- [ ] Withdraw pending report — removed from Pending Review list
- [ ] Logged-out user prompted to login on `/my/reports`
- [ ] Daily quota error on submit — clear message (from sub-phase 08/09)

### Phase 02 final gate

Re-run all items from [PHASE-02-report-submission.md](./PHASE-02-report-submission.md#8-acceptance-criteria) and [definition of done](./PHASE-02-report-submission.md#9-definition-of-done).

---

## 8. Exit criteria

- [ ] All Phase 02 acceptance criteria pass
- [ ] All API + UI smoke checks complete
- [ ] Mark sub-phase 10 and Phase 02 complete in [phase-02/README.md](./README.md)
- [ ] Ready to start Phase 03 — Admin Moderation
