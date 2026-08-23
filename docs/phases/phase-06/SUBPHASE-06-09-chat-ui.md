# Sub-phase 09 — Chat UI

**Status:** Not started  
**Prerequisites:** API sub-phases [02](./SUBPHASE-06-02-chat-read-api.md)–[08](./SUBPHASE-06-08-chat-notifications.md)

---

## 1. Summary

`/my/chats` list and `/my/chats/{threadId}` real-time chat view with SignalR client, safety banner, photo attachments, read-only mode.

---

## 2. Deliverables

### `/my/chats`

Thread list with counterparty, report title, last message, read-only badge.

### `/my/chats/{threadId}`

- Safety banner at top (every thread) linking to `/safety` (Phase 01 static page)
- Message list auto-scroll
- SignalR connect with JWT; `JoinThread` on enter, `LeaveThread` on destroy
- Text input + photo attach (disabled when `readOnlyAt` set)
- Pre-signed URL refresh on image error
- REST fallback if SignalR fails

### Enable chat link

Replace Phase 05 "قريباً" with link to thread from My Reports / notifications.

---

## 3. Exit criteria

- [ ] Real-time messaging without page refresh
- [ ] Safety banner visible
- [ ] Read-only thread: input disabled, history visible
- [ ] Mark complete in [phase-06/README.md](./README.md)
