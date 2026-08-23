# Sub-phase 04 — SignalR Chat Hub

**Status:** Not started  
**Prerequisites:** [Sub-phases 01–03](./SUBPHASE-06-01-signalr-contract.md)

---

## 1. Summary

Implement `ChatHub` at `/hubs/chat`: `JoinThread`, `LeaveThread`, `SendMessage`, broadcast `MessageReceived`. Persist `Message` rows. Track presence for notification suppression (sub-phase 08).

---

## 2. Deliverables

### Hub behavior

1. Authenticate connection (JWT)
2. `JoinThread` — verify participant; add connection to thread group; track presence
3. `SendMessage` — validate thread not read-only; validate body or attachment; persist `Message`; broadcast to group
4. `LeaveThread` — remove presence tracking
5. On cancel/resolve (later sub-phases): broadcast `ThreadReadOnly`

### Rules

- **No contact-info block** on message body (SPEC 5.6)
- Empty message rejected if no attachment
- Non-participant cannot join

### DI

Register SignalR in `Program.cs`; map hub endpoint; configure CORS for WebSockets.

---

## 3. Exit criteria

- [ ] Integration test: two connections send/receive message
- [ ] Message persisted in DB
- [ ] Non-participant rejected
- [ ] Mark complete in [phase-06/README.md](./README.md)
