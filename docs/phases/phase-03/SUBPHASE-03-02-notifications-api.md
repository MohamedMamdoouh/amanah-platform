# Sub-phase 02 — In-App Notification API

**Status:** Not started  
**Prerequisites:** [Sub-phase 01 — Decisions](./SUBPHASE-03-01-decisions.md), Phase 01 auth

---

## 1. Summary

Implement `INotificationService` and the notification center API: list, mark read, mark all read. Creates the persistence layer used by approve/reject in sub-phase 04. No moderation events wired yet — test with a dev-only seed endpoint or unit tests.

---

## 2. SPEC references

| SPEC section | Topic |
| ------------ | ----- |
| Section 5.7 | In-app notification center; unread until opened/marked read |
| Section 15.9 | Notification read behavior |
| Section 20.1 | Payload contract |

---

## 3. What you will learn

- JSON payload column on `Notification` entity
- User-scoped queries — users see only their own notifications
- `deepLink` routing contract for Angular (sub-phase 10)
- Idempotent mark-read operations

**Files to read after implementing:**

- `api/Services/Notifications/INotificationService.cs`
- `api/Services/Notifications/NotificationService.cs`
- `api/Controllers/NotificationsController.cs`
- `api/Dtos/Notifications/NotificationDto.cs`

---

## 4. Deliverables

### `INotificationService`

| Method | Purpose |
| ------ | ------- |
| `CreateAsync(userId, type, payload, ct)` | Insert unread notification |
| `GetForUserAsync(userId, page, pageSize, ct)` | Paginated list, newest first |
| `MarkReadAsync(userId, notificationId, ct)` | Set `ReadAt` if not already read |
| `MarkAllReadAsync(userId, ct)` | Mark all unread for user |
| `GetUnreadCountAsync(userId, ct)` | For header badge (optional now, used in sub-phase 10) |

### Endpoints

| Method | Route | Auth | Success |
| ------ | ----- | ---- | ------- |
| GET | `/api/notifications` | Required | `200` paginated list |
| GET | `/api/notifications/unread-count` | Required | `200` `{ "count": number }` |
| PATCH | `/api/notifications/{id}/read` | Required | `204` |
| POST | `/api/notifications/read-all` | Required | `204` |

### `GET /api/notifications`

**Query:** `page` (default 1), `pageSize` (default 20, max 50)

**Response item:**

| Field | Type |
| ----- | ---- |
| `id` | UUID |
| `type` | string |
| `payload` | object |
| `readAt` | datetime? |
| `createdAt` | datetime |

### Access rules

- User can only read/update own notifications
- `PATCH` on another user's notification → `404`
- Already-read notification: `PATCH` is idempotent `204`

### Test hook (dev/test only)

Optional internal test helper or `Testing` environment endpoint to create a sample notification — remove or guard before production.

---

## 5. Step-by-step implementation order

1. Review `Notification` entity from Phase 01 schema
2. Implement `NotificationService.CreateAsync` with JSON payload serialization
3. Implement list with pagination (`OrderByDescending CreatedAt`)
4. Implement mark read / mark all read
5. Add `NotificationsController` (list, unread-count, mark read, mark all)
6. Write integration tests: create via service, list, unread count, mark read, mark all
7. Verify unread count query works

---

## 6. Out of scope

- `ReportApproved` / `ReportRejected` event creation (sub-phase 04)
- Angular notification UI (sub-phase 10)
- Push notifications / WebSocket (not in v1)
- Email alerts (sub-phase 05)

---

## 7. Validation gate

### Automated tests

- [ ] Create notification → appears in user's list as unread
- [ ] User A cannot read user B's notifications
- [ ] `PATCH read` sets `readAt`; second call still `204`
- [ ] `POST read-all` marks all unread
- [ ] `GET /api/notifications/unread-count` returns correct count without loading full list
- [ ] Pagination returns correct page sizes
- [ ] Payload round-trips JSON correctly

### Manual smoke checklist

- [ ] Create test notification via test helper; `GET /api/notifications` returns it
- [ ] Mark read; list shows `readAt` populated

---

## 8. Exit criteria

- [ ] Notification API fully functional and tested
- [ ] Service ready for approve/reject to call in sub-phase 04
- [ ] Mark sub-phase 02 complete in [phase-03/README.md](./README.md)
