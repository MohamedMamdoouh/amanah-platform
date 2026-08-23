# Sub-phase 05 — Admin Email on New Submission

**Status:** Not started  
**Prerequisites:** [Sub-phase 01 — Decisions](./SUBPHASE-03-01-decisions.md), Phase 02 [Sub-phase 05 — Report Create](../phase-02/SUBPHASE-02-05-report-create.md)

---

## 1. Summary

Wire transactional email to alert the admin when a new report enters the moderation queue. Extends the existing report-create flow with a single hook — read the diff carefully. Uses `ConsoleEmailSender` locally and the real provider chosen in sub-phase 01 for production.

---

## 2. SPEC references

| SPEC section | Topic |
| ------------ | ----- |
| Section 5.7 | Email channel — admin only, new submissions |
| Section 21 | Transactional email infrastructure |

---

## 3. What you will learn

- Fire-and-forget vs awaited email — failure must not roll back report creation
- `IEmailSender` DI with environment-specific implementations
- Resubmit also re-enters queue — does email fire again? **Yes** — each `Pending Review` entry alerts admin
- Admin email address from environment variable (`ADMIN_EMAIL`)

**Files to read after implementing:**

- `api/Services/External/IEmailSender.cs`
- `api/Services/External/ConsoleEmailSender.cs`
- `api/Services/External/ResendEmailSender.cs` (or chosen provider)
- `api/Services/Reports/ReportSubmissionService.cs` — hook point only

---

## 4. Deliverables

### `IEmailSender` implementation

| Implementation | Environment | Behavior |
| -------------- | ----------- | -------- |
| `ConsoleEmailSender` | Development / Testing | Log `[EMAIL] Admin alert: N pending reports` |
| `ResendEmailSender` (or chosen) | Production | Send real email |

### Email content (minimal v1)

**Subject:** `Amanah: تقرير جديد بانتظار المراجعة`

**Body:** pending count + link to `/admin/moderation` (use `APP_BASE_URL` env var)

### Hook points

Call `IEmailSender.SendAdminModerationAlertAsync(pendingCount)` after:

1. **New report create** (`POST /api/reports`) — sub-phase 05 extension
2. **Resubmit** (`POST /api/reports/{id}/resubmit`) — implemented in sub-phase 07 (same `SendAdminModerationAlertAsync` hook)

**Error handling:**

- Email failure → log error; **do not** fail report creation
- No retry queue in v1 (acceptable per low volume)

### Configuration

| Env var | Purpose |
| ------- | ------- |
| `ADMIN_EMAIL` | Recipient |
| `RESEND_API_KEY` (or provider key) | Production only |
| `APP_BASE_URL` | Link in email body |

---

## 5. Step-by-step implementation order

1. Implement `ConsoleEmailSender`
2. Implement production sender for chosen provider
3. Register in DI by environment
4. Add `GetPendingCountAsync()` to `ModerationQueueService` (or inline query)
5. Add email hook at end of `ReportSubmissionService.CreateAsync` (after transaction commits)
6. Integration test: create report → console sender invoked once
7. Integration test: email throws → report still `201`

---

## 6. Out of scope

- User-facing email (never in v1)
- Email on approve/reject (in-app only for reporters)
- Resubmit hook (can add in sub-phase 07 — note in README)
- Digest emails / batching

---

## 7. Validation gate

### Automated tests

- [ ] Report create triggers `IEmailSender` with correct pending count
- [ ] Email sender throws → report still created successfully
- [ ] `ConsoleEmailSender` writes to log (capture in test)
- [ ] Testing environment uses fake/console sender, not real API

### Manual smoke checklist

- [ ] Submit report locally — email line appears in API console
- [ ] Pending count in email matches `GET /api/admin/moderation/queue` `pendingCount`

---

## 8. Exit criteria

- [ ] Admin alerted on every new pending submission
- [ ] Report creation never fails due to email
- [ ] Mark sub-phase 05 complete in [phase-03/README.md](./README.md)
