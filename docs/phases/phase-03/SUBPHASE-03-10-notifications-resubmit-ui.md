# Sub-phase 10 — Notification Center & Rejected Report Flow

**Status:** Not started  
**Prerequisites:** [Sub-phase 02](./SUBPHASE-03-02-notifications-api.md), [04](./SUBPHASE-03-04-approve-reject.md), [07](./SUBPHASE-03-07-resubmit-api.md), Phase 02 My Reports shell

---

## 1. Summary

Build `/notifications` notification center, expand My Reports with **Rejected** and **Published** tabs, and the rejected-report edit/resubmit flow. Reporters see rejection reasons, fix content, and resubmit. Unread notification badge in app header.

---

## 2. SPEC references

| SPEC section | Topic |
| ------------ | ----- |
| Section 4.3 | Rejected tab — read reason, fix, resubmit |
| Section 4.8 | My Reports status tabs |
| Section 5.7 | Read behavior; ReportApproved/Rejected payloads |
| Section 15.9 | Notifications unread until opened/marked read |

---

## 3. What you will learn

- Notification list with `deepLink` navigation
- Mark read on open vs explicit mark-all
- Reusing report form components from Phase 02 for edit mode
- Rejection reason display with Arabic labels
- Resubmission cap UX (disable resubmit after 3rd rejection)
- Published tab read-only (detail links — public URL in Phase 04)

**Files to read after implementing:**

- `web/src/app/features/notifications/notifications-page/`
- `web/src/app/core/services/notifications.service.ts`
- `web/src/app/shared/components/notification-bell/`
- `web/src/app/features/my-reports/rejected-report-edit-page/`
- `web/src/app/features/my-reports/my-reports-page/` (tab expansion)

---

## 4. Deliverables

### Page: `/notifications`

- Paginated list, newest first
- Each item: icon by type, Arabic summary text, relative time
- Unread items visually distinct
- Click item → navigate to `payload.deepLink` + `PATCH` mark read
- "Mark all read" button → `POST /api/notifications/read-all`
- Empty state

### Notification bell (app header)

- Unread count badge
- Click → `/notifications`
- Refresh count on navigation / interval (simple: fetch on route change)

### Arabic copy examples

| Type | Summary |
| ---- | ------- |
| `ReportApproved` | تمت الموافقة على بلاغك "{title}" |
| `ReportRejected` | تم رفض بلاغك "{title}" — يمكنك التعديل وإعادة الإرسال |

### My Reports tab expansion

| Tab | Phase 03 behavior |
| --- | ----------------- |
| Pending Review | Existing from Phase 02 |
| **Rejected** | List rejected reports with reason badge |
| **Published** | List published reports (read-only); each row links to `/lost/{id}` or `/found/{id}` (verified in Phase 04 sub-08) |
| Claim In Progress | Disabled stub until Phase 05 |
| Closed | Disabled stub until Phase 07 |

### Page: `/my/reports/{id}` — rejected state

When report status is `Rejected`:

- Show rejection reason (Arabic label) + admin note
- **Edit** mode — reuse lost/found form components pre-filled
- **Resubmit** button → `POST /api/reports/{id}/resubmit` after save
- Show resubmissions remaining: `3 - resubmissionCount`
- After cap exceeded: edit disabled, message "لا يمكن إعادة الإرسال مرة أخرى"
- Hidden verification detail editable (reporter only)

### Page: `/my/reports/{id}` — published state

- Read-only detail (no edit, no withdraw yet — Phase 07)
- Link to public URL `/lost/{id}` or `/found/{id}` by report type (integration test in Phase 04 sub-08)

### `NotificationsService`

| Method | API |
| ------ | --- |
| `getNotifications(page)` | `GET /api/notifications` |
| `markRead(id)` | `PATCH /api/notifications/{id}/read` |
| `markAllRead()` | `POST /api/notifications/read-all` |
| `getUnreadCount()` | `GET /api/notifications/unread-count` |

---

## 5. Step-by-step implementation order

1. Implement `NotificationsService`
2. Build notifications page
3. Add notification bell to header layout
4. Extend My Reports page — Rejected + Published tabs
5. Extend report detail — rejection banner with reason + note
6. Build edit mode (reuse Phase 02 form components with `PUT` save)
7. Add resubmit button and cap messaging
8. Wire notification click → deep link to rejected report
9. E2E: reject as admin → reporter sees notification → edits → resubmits

---

## 6. Out of scope

- Claim In Progress tab (Phase 05)
- Withdraw published report (Phase 07)
- Public report detail pages (Phase 04)
- Admin moderation UI (sub-phase 09)

---

## 7. Validation gate

### Automated tests

- [ ] Notification list renders unread state
- [ ] Click notification calls markRead + navigates
- [ ] Rejected tab filters correctly
- [ ] Resubmit button disabled when cap reached
- [ ] Edit form calls PUT with updated fields

### Manual smoke checklist

- [ ] Approve report → reporter sees `ReportApproved` notification
- [ ] Reject with reason → reporter sees reason in Rejected tab and notification
- [ ] Edit rejected report title → save → resubmit → back to Pending Review
- [ ] After 3 resubmission rejections → resubmit blocked with clear message
- [ ] Mark all read clears badge
- [ ] Published tab shows approved reports

### Phase 03 reporter-flow gate

Re-run SPEC 15.2 items for reject, resubmit, and notification behavior.

---

## 8. Exit criteria

- [ ] Notification center and reporter resubmit flow complete
- [ ] Mark sub-phase 10 complete in [phase-03/README.md](./README.md)
