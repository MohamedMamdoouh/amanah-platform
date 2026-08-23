# Sub-phase 01 — SignalR Contract

**Status:** Not started  
**Prerequisites:** Phase 05 complete

---

## 1. Summary

Document the SignalR hub contract in `docs/signalr-contract.md` and add TypeScript/C# DTO stubs. No hub implementation yet — contract-first per Section 14.

---

## 2. Deliverables

Create `docs/signalr-contract.md` with:

| Direction | Method/Event | Payload |
| --------- | ------------ | ------- |
| Client → Server | `JoinThread` | `{ threadId }` |
| Client → Server | `LeaveThread` | `{ threadId }` |
| Client → Server | `SendMessage` | `{ threadId, body?, attachmentId? }` |
| Server → Client | `MessageReceived` | `{ messageId, threadId, senderId, senderDisplayName, body, attachmentUrl?, createdAt }` |
| Server → Client | `ThreadReadOnly` | `{ threadId, readOnlyAt }` |
| Server → Client | `ResolutionUpdated` | `{ reportId, reporterConfirmed, claimantConfirmed }` |

**Auth:** JWT via `accessTokenFactory` or query `access_token`.

Add stub files: `api/Hubs/ChatHub.cs` (empty), `web/src/app/core/signalr/chat-hub.types.ts`.

---

## 3. Exit criteria

- [ ] Contract doc committed and reviewed
- [ ] Mark complete in [phase-06/README.md](./README.md)
