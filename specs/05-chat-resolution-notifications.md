# Phase 05 - Chat, Resolution & Notifications

**Status:** Complete — code and automated tests shipped; manual smoke checklist ready for QA  
**Prerequisites:** Phase 04 - Claims & Verification

---

## 1. Summary

Activate real-time in-app chat via SignalR for approved claims, with text and photo attachments, safety banner, and message rate limits. Implement mutual Confirm Resolved flow (irrevocable confirmations) and claim cancellation before resolution. Add **remaining** in-app notification event types (the notification center and moderation events shipped in [Phase 02](./02-admin-moderation.md)) including `NewChatMessage` with view-suppression. Chat threads become read-only on cancellation or resolution. Thirty-day deletion shipped in Phase 06.

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
| Section 15.5 | Resolution and chat acceptance criteria (chat retention shipped in Phase 06) |
| Section 15.9 | Notifications acceptance criteria |
| Section 16 | SignalR for real-time chat |
| Section 20.1 | Notification payload contract |
| [05-signalr-contract.md](./05-signalr-contract.md) | SignalR hub methods, events, presence, REST fallback |

**Part II (technical):** Section 16 (SignalR), Section 20.1

---

## 3. Prerequisites

### Prior phases

- [x] Phase 00 - Platform foundation
- [x] Phase 01 - Report Submission
- [x] Phase 02 - Admin Moderation
- [x] Phase 04 - Claims & Verification (`ChatThread` rows created on claim approval; messaging activated in this phase)

### Deferred decisions (Section 14)

| Item | Notes |
| ---- | ----- |
| SignalR event/payload contract | **Resolved** — [05-signalr-contract.md](./05-signalr-contract.md) |

---

## 4. Deliverables

### API

| Method | Route | Purpose |
| ------ | ----- | ------- |
| GET | `/api/v1/chats` | List user's chat threads (My Chats) |
| GET | `/api/v1/chats/{threadId}` | Thread metadata + message history |
| POST | `/api/v1/chats/{threadId}/messages` | Send message (REST fallback; primary via SignalR) |
| POST | `/api/v1/uploads/chat-attachment` | Upload chat photo attachment |
| GET | `/api/v1/uploads/chat-attachment/{id}/url` | Refresh pre-signed URL for attachment |
| POST | `/api/v1/claims/{id}/confirm-resolution` | Party confirms item returned |
| POST | `/api/v1/claims/{id}/cancel` | Cancel approved claim before mutual confirm |
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

These rows are server-enforced:

| Data | Approved claimant | Reporter | Admin |
| ---- | ----------------- | -------- | ----- |
| Chat thread | yes | yes | yes (flagged-listing investigation only — open abuse flag on listing) |
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
| Claim ended by enforcement | Affected party | Shipped (Phase 07) |
| Admin takedown affecting you | Reporter and claimant | Shipped (Phase 07) |

Claim/report notification types from Phases 02–04 (`ReportApproved`, `ReportRejected`, `NewClaimSubmitted`, `ClaimWithdrawnByClaimant`, `ClaimApproved`, `ClaimRejected`, `ClaimClosedReportUnavailable`) remain active. This phase adds chat and resolution events.

---

## 7. Out of scope

Shipped in Phase 07: the report-from-chat flag shortcut on `/my/chats/{threadId}`.

---

## 8. Acceptance criteria

From [SPEC.md Section 15.5](./SPEC.md#155-resolution-and-chat).

- [x] **Mutual confirmation is the only resolve path:** the report becomes `Resolved` only when both parties have confirmed. Reporter-only close and one-sided timeout resolution do not exist
- [x] **Confirmation is irrevocable:** a party who has confirmed cannot un-confirm and cannot cancel the claim; the other party can still confirm or cancel
- [x] **Confirmation notifications:** the first confirmation notifies the counterparty that their confirmation is awaited, and the second notifies both parties that the report is resolved
- [x] **Cancellation path:** cancelling before mutual confirmation sets the claim to `Cancelled`, returns the report to `Published`, notifies the counterparty, and makes the chat read-only immediately
- [x] **Chat reachability:** both parties can still open a read-only thread from My Chats while it exists, even though the report's public URL is unavailable

**Shipped in Phase 06** ([06-lifecycle-retention.md](./06-lifecycle-retention.md) #8): chat retention deletes a read-only thread 30 days after it becomes read-only.

From [SPEC.md Section 15.9](./SPEC.md#159-notifications).

- [x] Each event in the Section 5.7 table produces exactly one in-app notification for each listed recipient, deep-linking to the relevant report, claim, or thread (for all events implemented to date)
- [x] A new-message notification is suppressed while the recipient is viewing that same thread
- [x] Notifications remain unread until opened or explicitly marked read, and no setting can disable any of them
- [x] SMS is sent only for OTP; email is sent only to the admin for pending submissions

---

## 9. Definition of done

### Automated tests

- [x] SignalR: send/receive text message in thread
- [x] Photo attachment upload and pre-signed URL in message
- [x] Safety banner shown on new thread
- [x] First confirm: counterparty notified, report still `Claim In Progress`
- [x] Second confirm: report -> `Resolved`, both notified
- [x] Irrevocable confirm: confirmer cannot cancel
- [x] Cancel before confirm: report -> `Published`, chat read-only
- [x] `NewChatMessage` suppressed while viewing thread
- [x] Chat rate limit (10/min). The 60/hour policy is configured; see known gaps below
- [x] Non-participant cannot access thread

### Manual smoke checklist

- [ ] Approve claim; open chat; send text and photo in real time (no page refresh)
- [ ] Safety banner visible; links to safety page
- [ ] Both parties confirm resolved; report status updates
- [ ] Cancel claim; chat becomes read-only; report claimable again
- [ ] My Chats lists active and read-only threads
- [ ] Notification center shows all event types with correct deep links

### Known gaps

- Orphan chat attachment cleanup after a failed send (staging row with `MessageId == null`) — follow-up hardening, not blocking QA
- Hourly chat rate limit (60/hour) is configured but only per-minute limit is covered in automated tests

### Phase exit gate

Phase 05 code and automated tests are complete. Remaining manual smoke is QA before production. Phase 06 shipped chat retention.
