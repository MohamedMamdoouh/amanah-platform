# Sub-phase 02 — Chat Read API

**Status:** Not started  
**Prerequisites:** [Sub-phase 01](./SUBPHASE-06-01-signalr-contract.md), Phase 05 `ChatThread` records

---

## 1. Summary

`GET /api/chats` (My Chats list) and `GET /api/chats/{threadId}` (metadata + paginated message history). Participant-only access.

---

## 2. Deliverables

### `GET /api/chats`

List threads for current user (reporter or claimant on linked claim): counterparty name, report title, last message preview, `readOnlyAt`, `unread` hint optional.

### `GET /api/chats/{threadId}`

Thread metadata + messages (newest last or first — document order). Include `readOnlyAt`, `reportId`, `claimId`.

### Access

Non-participant → `404`. Works for read-only threads (post-cancel/resolve).

---

## 3. Exit criteria

- [ ] Participant can list and load history
- [ ] Non-participant → `404`
- [ ] Mark complete in [phase-06/README.md](./README.md)
