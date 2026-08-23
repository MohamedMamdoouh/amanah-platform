# Sub-phase 05 — Report Create API

**Status:** Not started  
**Prerequisites:** [Sub-phase 02](./SUBPHASE-02-02-validation-utilities.md), [03](./SUBPHASE-02-03-quota-service.md), [04](./SUBPHASE-02-04-photo-upload.md)

---

## 1. Summary

Implement `POST /api/reports` — the core submission endpoint for lost and found reports. Wires validation utilities, quota service, photo linking, hidden verification detail storage, and normalized search text. Creates reports in `Pending Review` status.

---

## 2. SPEC references

| SPEC section | Topic |
| ------------ | ----- |
| Section 4.1–4.2 | Lost vs found fields |
| Section 4.1.3 | Contact-info block |
| Section 4.1.6–4.1.7 | Quotas |
| Section 5.2–5.4 | Categories, location, reward, hidden detail |
| Section 9 | Hidden detail never to admin |
| Section 15.1 | Submission acceptance criteria |
| Section 16 | Search column on write |

---

## 3. What you will learn

- Transactional report create: `Report` + `CategoryField` rows + link `ReportPhoto`
- Mapping validation errors to field-level API responses
- Storing hidden verification detail separately in response DTOs
- Found-only fields: `itemHeldLocation` enum + detail text
- Initializing `resubmissionCount = 0`, `status = Pending Review`

**Files to read after implementing:**

- `api/Controllers/ReportsController.cs` (create action only)
- `api/Services/Reports/ReportSubmissionService.cs`
- `api/Dtos/Reports/CreateReportRequest.cs`, `ReportResponse.cs`
- `api.Tests/Controllers/ReportsControllerCreateTests.cs`

---

## 4. Deliverables

### Endpoint

| Method | Route | Auth | Success |
| ------ | ----- | ---- | ------- |
| POST | `/api/reports` | Required | `201` + report summary |

### Request body

| Field | Lost | Found | Rules |
| ----- | ---- | ----- | ----- |
| `type` | `"lost"` | `"found"` | Required enum |
| `categoryId` | ✓ | ✓ | Active category |
| `title` | ✓ | ✓ | 10–80 chars; contact-info block |
| `description` | ✓ | ✓ | 20–1,000 chars; contact-info block |
| `dateLostOrFound` | ✓ | ✓ | `YYYY-MM-DD`; Cairo bounds |
| `governorateId` | ✓ | ✓ | Valid governorate |
| `area` | optional | optional | Max 120; contact-info block |
| `categoryFields` | ✓ | ✓ | Map of field key → value |
| `photoIds` | optional | optional | 0–5; must be uploaded by same user, unlinked |
| `rewardOffered` | ✓ | ✓ | Boolean |
| `rewardAmount` | conditional | conditional | Required if flag true; 50–50,000 |
| `itemHeldLocation` | — | ✓ | Enum: WithFinder, PoliceStation, BuildingSecurity, Workplace, Other |
| `itemHeldLocationDetail` | — | conditional | Required when Other; max 120; contact-info block |
| `hiddenVerificationDetail` | ✓ | ✓ | 10–500 chars; **no** contact-info block |

### Create flow

1. Authenticate → `401` if missing
2. `SubmissionQuotaService.CheckNewSubmissionAsync` → `429` if blocked
3. Normalize all text via `TextNormalizer`
4. Validate core fields, dates, reward, category fields, contact-info (sub-phase 02)
5. Validate `photoIds`: count ≤ 5, owned by user, not already linked, **uploaded for the same `categoryId` as this report** (reject photos routed to the wrong bucket if the user changed category after upload)
6. Validate category exists and is active
7. **Transaction:**
   - Insert `Report` with `Status = Pending Review`, `ResubmissionCount = 0`
   - Insert `CategoryField` rows
   - Link `ReportPhoto` rows to report
   - Set `normalizedSearchText` via `ReportSearchTextBuilder`
8. Return `201` with report DTO — **exclude** `hiddenVerificationDetail` from default response (include only in reporter-specific detail endpoint in sub-phase 06)

### Response DTO (create)

Include: `id`, `type`, `status`, `title`, `category`, `createdAt`. Do not include hidden detail in list/summary shapes.

### Error responses

| Condition | Status | Code |
| --------- | ------ | ---- |
| Field validation | `400` | `validation.failed` + `errors` object |
| Quota daily | `429` | `report.quota_daily_exceeded` |
| Open cap | `429` | `report.quota_open_cap_exceeded` |
| Invalid category/photo | `400` | appropriate code |

---

## 5. Step-by-step implementation order

1. Define `CreateReportRequest` and validation attributes where helpful
2. Implement `ReportSubmissionService.CreateAsync`
3. Wire validators from sub-phase 02
4. Wire quota service from sub-phase 03
5. Photo linking logic with ownership check
6. Add `ReportsController.Post` only
7. Write integration tests: lost minimal, found full, all validation failures
8. Test search column persisted correctly in DB after create

---

## 6. Out of scope

- `GET /api/reports/*` (sub-phase 06)
- Resubmit rejected report (Phase 03)
- Admin moderation endpoints (Phase 03)
- Confirmation screen UI (sub-phases 08–09)
- Notifications on submit (Phase 03)

---

## 7. Validation gate

### Automated tests — happy path

- [ ] Valid lost report (phones category, 2 photos) → `201`, status `Pending Review`
- [ ] Valid found report with held location `Other` + detail → `201`
- [ ] `normalizedSearchText` populated and normalized
- [ ] `CategoryField` rows created matching request
- [ ] Photos linked to report

### Automated tests — validation

- [ ] Future date → field error on `dateLostOrFound`
- [ ] Title with `wa.me/123` → contact-info field error
- [ ] Hidden detail with phone number → **accepted**
- [ ] Missing hidden detail → field error
- [ ] 6 photo IDs → rejected
- [ ] Photo uploaded by another user → rejected
- [ ] Invalid category field (key count 25) → field error
- [ ] Reward flag true, amount 30 → field error

### Automated tests — quota

- [ ] 4th submission same day → `429 report.quota_daily_exceeded`
- [ ] 6th open report → `429 report.quota_open_cap_exceeded`

### Automated tests — permissions

- [ ] Admin API response for created report does **not** include hidden detail (test via service layer or future GET)

### Manual smoke checklist

- [ ] `curl` POST with JWT — inspect DB row for `normalized_search_text`
- [ ] Confirm `resubmission_count = 0`

---

## 8. Exit criteria

- [ ] All automated tests pass (SPEC 15.1 API coverage)
- [ ] Create endpoint is the only write path for new reports
- [ ] Mark sub-phase 05 complete in [phase-02/README.md](./README.md)
