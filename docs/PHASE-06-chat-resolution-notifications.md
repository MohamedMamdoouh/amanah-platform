# Phase 06 - Chat, Resolution & Notifications

**Status:** Not started  
**Prerequisites:** Phase 05 - Claims & Verification

---

## 1. Summary

Activate real-time in-app chat via SignalR for approved claims, with text and photo attachments, safety banner, and message rate limits. Implement mutual Confirm Resolved flow (irrevocable confirmations) and claim cancellation before resolution. Complete the notification center with all remaining in-app event types including `NewChatMessage` with view-suppression. Chat threads become read-only on cancellation or resolution; 30-day deletion is deferred to Phase 07.

---

## 2. SPEC references

| SPEC section | Topic |
| ------------ | ----- |
| Section 4.6 | Handover and resolution |
| Section 4.8 | My Chats |
| Section 5.6 | Messaging |
| Section 5.7 | Full in-app notification events |
| Section 7.3 | Safety banner and safety page |
| Section 7.5 | Chat message rate limits |
| Section 15.5 | Resolution and chat acceptance criteria (retention deletion deferred) |
| Section 15.9 | Notifications acceptance criteria |
| Section 16 | SignalR for real-time chat |
| Section 20.1 | Notification payload contract |

**Part II (technical):** Section 16 (SignalR), Section 20.1

---

## 3. Prerequisites

### Prior phases

- [ ] Phase 01 - Platform Foundation
- [ ] Phase 05 - Claims & Verification (`ChatThread` records exist on approved claims)

### Deferred decisions (Section 14)

Resolve **before starting** this phase:

| Item | Notes |
| ---- | ----- |
| SignalR event/payload contract | Define in `docs/signalr-contract.md` before starting this phase |

---

## 4. Deliverables

### API

| Method | Route | Purpose |
| ------ | ----- | ------- |
| GET | `/api/chats` | List user's chat threads (My Chats) |
| GET | `/api/chats/{threadId}` | Thread metadata + message history |
| POST | `/api/chats/{threadId}/messages` | Send message (REST fallback; primary via SignalR) |
| POST | `/api/uploads/chat-attachment` | Upload chat photo attachment |
| GET | `/api/uploads/chat-attachment/{id}/url` | Refresh pre-signed URL for attachment |
| POST | `/api/claims/{id}/confirm-resolution` | Party confirms item returned |
| POST | `/api/claims/{id}/cancel` | Cancel approved claim before mutual confirm |
| Hub | `/hubs/chat` | SignalR real-time messaging |

### UI routes

| Route | Access | Purpose |
| ----- | ------ | ------- |
| `/my/chats` | Logged-in | Chat thread list |
| `/my/chats/{threadId}` | Logged-in (participant) | Chat view with safety banner |
| `/lost/{id}` / `/found/{id}` | Logged-in | Confirm Resolved button when claim approved |

### Database

- `Message` records linked to `ChatThread`
- `Resolution` record: `reporterConfirmedAt`, `claimantConfirmedAt`, `resolvedAt`
- `ChatThread.readOnlyAt` set on cancellation or resolution
- `Report.status` -> `Resolved` when both parties confirm

### Infrastructure

- SignalR hub on ASP.NET Core API
- WebSocket/long-polling fallback for mobile browsers

### Shared utilities

- Safety banner component on every new thread (links to `/safety`)
- `NewChatMessage` notification suppressed while recipient has thread open (track via `JoinThread`/`LeaveThread` or presence)
- Pre-signed URL refresh for chat attachments (5-minute expiry, silent retry)
- Chat rate limits: 10/min, 60/hour per account
- Irrevocable confirmation: party who confirmed cannot cancel; other party can still confirm or cancel
- Cancel approved claim -> `Cancelled`, report -> `Published`, chat read-only

---

## 5. Permissions (Section 9)

Server-enforce these matrix rows before marking this phase done:

| Data | Approved claimant | Reporter | Admin |
| ---- | ----------------- | -------- | ----- |
| Chat thread | yes | yes | yes (flagged-listing investigation only - Phase 08) |
| Claim text and photo | own + reporter | yes | investigation only |
| Display names | yes | yes | yes |
| Phone numbers | own | own | yes |

Chat messages are **not** subject to contact-info block (Section 4.1.3).

---

## 6. Notifications (Section 5.7)

| Event | Recipient | Introduced |
| ----- | --------- | ---------- |
| Claim cancelled by counterparty | Other party | this phase |
| Counterparty confirmed resolution | Other party | this phase |
| Report resolved | Both parties | this phase |
| New chat message | Recipient | this phase |
| Claim ended by enforcement | Affected party | deferred to Phase 08 |
| Admin takedown affecting you | Reporter and claimant | deferred to Phase 08 |

All previously introduced claim/report notifications from Phases 03-05 remain active.

---

## 7. Out of scope

Explicitly deferred to later phases:

- 30-day chat deletion job -> Phase 07
- Report-from-chat abuse shortcut -> Phase 08
- Admin chat access during investigation -> Phase 08
- Claim-ended-by-enforcement -> Phase 08

---

## 8. Acceptance criteria

From [SPEC.md Section 15.5](./SPEC.md#155-resolution-and-chat).

- [ ] **Mutual confirmation is the only resolve path:** the report becomes `Resolved` only when both parties have confirmed. Reporter-only close and one-sided timeout resolution do not exist
- [ ] **Confirmation is irrevocable:** a party who has confirmed cannot un-confirm and cannot cancel the claim; the other party can still confirm or cancel
- [ ] **Confirmation notifications:** the first confirmation notifies the counterparty that their confirmation is awaited, and the second notifies both parties that the report is resolved
- [ ] **Cancellation path:** cancelling before mutual confirmation sets the claim to `Cancelled`, returns the report to `Published`, notifies the counterparty, and makes the chat read-only immediately
- [ ] **Chat reachability:** both parties can still open a read-only thread from My Chats while it exists, even though the report's public URL is unavailable

**Deferred within v1:**

- [ ] **Chat retention (30-day delete)** -> Phase 07

From [SPEC.md Section 15.9](./SPEC.md#159-notifications).

- [ ] Each event in the Section 5.7 table produces exactly one in-app notification for each listed recipient, deep-linking to the relevant report, claim, or thread (for all events implemented to date)
- [ ] A new-message notification is suppressed while the recipient is viewing that same thread
- [ ] Notifications remain unread until opened or explicitly marked read, and no setting can disable any of them
- [ ] SMS is sent only for OTP; email is sent only to the admin for pending submissions

---

## 9. Definition of done

### Automated tests

- [ ] SignalR: send/receive text message in thread
- [ ] Photo attachment upload and pre-signed URL in message
- [ ] Safety banner shown on new thread
- [ ] First confirm: counterparty notified, report still `Claim In Progress`
- [ ] Second confirm: report -> `Resolved`, both notified
- [ ] Irrevocable confirm: confirmer cannot cancel
- [ ] Cancel before confirm: report -> `Published`, chat read-only
- [ ] `NewChatMessage` suppressed while viewing thread
- [ ] Chat rate limits (10/min, 60/hour)
- [ ] Non-participant cannot access thread

### Manual smoke checklist

- [ ] Approve claim; open chat; send text and photo in real time (no page refresh)
- [ ] Safety banner visible; links to safety page
- [ ] Both parties confirm resolved; report status updates
- [ ] Cancel claim; chat becomes read-only; report claimable again
- [ ] My Chats lists active and read-only threads
- [ ] Notification center shows all event types with correct deep links

### Phase exit gate

This phase is complete when all acceptance criteria pass and no out-of-scope items were implemented early.
