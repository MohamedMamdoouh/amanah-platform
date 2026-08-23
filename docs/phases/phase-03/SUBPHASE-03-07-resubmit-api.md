# Sub-phase 07 — Reporter Edit & Resubmit API

**Status:** Not started  
**Prerequisites:** [Sub-phase 04 — Approve/Reject](./SUBPHASE-03-04-approve-reject.md), Phase 02 validators and quota service

---

## 1. Summary

Allow reporters to edit and resubmit `Rejected` reports: `PUT` for content changes, `POST /resubmit` to return to `Pending Review`. Re-runs validation and contact-info block, exempts daily quota, enforces 3-resubmission cap, and re-derives photo privacy on category change.

---

## 2. SPEC references

| SPEC section | Topic |
| ------------ | ----- |
| Section 4.3 | Fix and resubmit; max 3 resubmissions |
| Section 4.1.6 | Resubmit does not count against daily quota |
| Section 5.2 | Photo privacy re-derived on category change |
| Section 15.2 | Resubmit acceptance criteria |

---

## 3. What you will learn

- Two-step edit vs resubmit: edit saves while `Rejected`; resubmit transitions status
- Or single-step: `PUT` only edits, `POST resubmit` validates + transitions — **recommended:** `PUT` updates content in place; `POST resubmit` validates all fields and moves to `Pending Review`
- Incrementing `resubmissionCount` on each resubmit (not on edit)
- Moving photos between public/private bucket prefixes on category change
- Extending `SubmissionQuotaService` with resubmit bypass

**Files to read after implementing:**

- `api/Services/Reports/ReportResubmitService.cs`
- `api/Controllers/ReportsController.cs` (put + resubmit)
- `api/Services/Reports/SubmissionQuotaService.cs` — add resubmit exemption
- `api/Services/Media/PhotoPrivacyService.cs` (new — bucket move on category change)

---

## 4. Deliverables

### Endpoints

| Method | Route | Auth | Purpose |
| ------ | ----- | ---- | ------- |
| PUT | `/api/reports/{id}` | Reporter (owner) | Edit `Rejected` report content |
| POST | `/api/reports/{id}/resubmit` | Reporter (owner) | Validate + → `Pending Review` |

### `PUT /api/reports/{id}`

**Allowed status:** `Rejected` only → else `409 report.invalid_status`

**Editable fields:** same as create (title, description, date, category, fields, photos, reward, held location, hidden detail)

**Flow:**

1. Verify owner
2. Re-run all validators from Phase 02 (contact-info, fields, dates, reward)
3. Update report + category fields
4. Recompute `normalizedSearchText`
5. If `categoryId` changed → `PhotoPrivacyService.RederiveAsync(report)` moves photos between public/private storage
6. Status stays `Rejected` — reporter can edit multiple times before resubmitting
7. Return reporter detail DTO (includes hidden detail + rejection reason)

### `POST /api/reports/{id}/resubmit`

**Allowed status:** `Rejected` only

**Pre-checks:**

- `resubmissionCount < 3` → else `409 report.resubmission_cap_exceeded`
- **No** daily quota check (resubmit exempt)
- **No** open-report cap check (resubmit of existing report)

**Flow:**

1. Verify owner
2. Re-validate all fields (contact-info block runs again)
3. `resubmissionCount++`
4. `Status` → `Pending Review`
5. Clear `RejectionReason` / `RejectionNote` / `RejectedAt` (or keep for history on `ModerationAction` — prior rejections are in audit table)
6. Trigger admin email (sub-phase 05 hook)
7. Return `200` with report summary

### Photo privacy re-derivation

When category changes between `photosPrivate` true/false:

- Move objects in storage from public → private prefix or vice versa
- Update `ReportPhoto` storage keys
- Integration test: phones → documents-ids makes photos private

### Extend reporter read API (Phase 02 sub-phase 06)

- `GET /api/reports/mine?status=rejected` — include rejection reason + note in list/detail
- `GET /api/reports/mine?status=published` — published reports for My Reports tab

### Edit refused tests

`PUT` on `Pending Review`, `Published`, `Claim In Progress`, terminal → `409`

---

## 5. Step-by-step implementation order

1. Extend `SubmissionQuotaService` — document resubmit bypass (may already be stubbed from Phase 02)
2. Implement `PhotoPrivacyService.RederiveAsync`
3. Implement `ReportResubmitService.UpdateRejectedAsync` (PUT logic)
4. Implement `ReportResubmitService.ResubmitAsync` (POST logic)
5. Extend `ReportsController`
6. Extend `GET /api/reports/mine` for rejected + published filters
7. Wire admin email on resubmit
8. Comprehensive integration tests per SPEC 15.2

---

## 6. Out of scope

- Resubmit UI (sub-phase 10)
- Admin re-review after resubmit (uses existing queue from sub-phase 03)
- 30-day rejected deletion job (Phase 07)

---

## 7. Validation gate

### Automated tests

- [ ] PUT on rejected report updates title; status stays `Rejected`
- [ ] PUT on pending report → `409`
- [ ] PUT on published report → `409`
- [ ] POST resubmit → `Pending Review`, `resubmissionCount` incremented
- [ ] Resubmit does not increment daily quota counter
- [ ] Resubmit succeeds when user is at open-report cap (5)
- [ ] 4th resubmit attempt (after 3 resubmissions) → `409 resubmission_cap_exceeded`
- [ ] Contact-info in title on resubmit → `400` field error
- [ ] Category change to `photosPrivate` moves photos to private prefix
- [ ] Category change from private to public moves photos to public prefix
- [ ] `normalizedSearchText` updated on edit
- [ ] Non-owner PUT/POST → `404`

### Manual smoke checklist

- [ ] Reject report → reporter PUT edits title → POST resubmit → appears in admin queue again
- [ ] Resubmit triggers admin email (console log)

---

## 8. Exit criteria

- [ ] All SPEC 15.2 resubmit criteria pass at API level
- [ ] Photo privacy re-derivation verified
- [ ] Mark sub-phase 07 complete in [phase-03/README.md](./README.md)
