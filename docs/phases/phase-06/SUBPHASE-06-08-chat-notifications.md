# Sub-phase 08 — Chat & Resolution Notifications

**Status:** Not started  
**Prerequisites:** [Sub-phases 04–07](./SUBPHASE-06-04-signalr-hub.md), Phase 03 notification service

---

## 1. Summary

Wire `NewChatMessage` with **view suppression** while recipient has thread open (via `JoinThread` presence). Verify all Phase 06 notification types. Audit Section 5.7 events implemented to date (Phases 03–06).

---

## 2. Deliverables

### `NewChatMessage` suppression

- On send: if recipient **not** in thread presence set → create notification
- If recipient connected and `JoinThread` active for that thread → skip notification
- `deepLink`: `/my/chats/{threadId}`

### Notification regression checklist

Verify one notification per event for: `ReportApproved`, `ReportRejected`, `NewClaimSubmitted`, `ClaimWithdrawnByClaimant`, `ClaimApproved`, `ClaimRejected`, `ClaimClosedReportUnavailable`, `ClaimCancelledByCounterparty`, `CounterpartyConfirmedResolution`, `ReportResolved`, `NewChatMessage`.

### Deep links (Phase 06 events)

| Type | `deepLink` | Optional payload IDs |
| ---- | ---------- | -------------------- |
| `ClaimCancelledByCounterparty` | Report detail (`/lost/{id}` or `/found/{id}`) | `reportId`, `claimId` |
| `CounterpartyConfirmedResolution` | Same report detail | `reportId`, `claimId` |
| `ReportResolved` | Same report detail | `reportId` |
| `NewChatMessage` | `/my/chats/{threadId}` | `chatThreadId`, `reportId` |

### Section 15.9

- Unread until opened/marked read (Phase 03 behavior — regression test)
- No SMS or email sent for any Phase 06 notification event (SMS OTP-only; admin email on new submission only — regression test)
- Phone numbers never returned to another user in chat/report APIs (§9 regression — no new surface in Phase 06)

---

## 3. Exit criteria

- [ ] Message notification suppressed while viewing thread
- [ ] Notification created when thread not open
- [ ] Section 5.7 regression tests pass for implemented events
- [ ] No SMS/email for chat/resolution events (§15.9 channel rules)
- [ ] Mark complete in [phase-06/README.md](./README.md)
