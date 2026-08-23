# Phase 06 — Sub-phases

This directory contains [PHASE-06-chat-resolution-notifications.md](./PHASE-06-chat-resolution-notifications.md), broken into **10 incremental sub-phases**.

**Parent phase:** [PHASE-06 — Chat, Resolution & Notifications](./PHASE-06-chat-resolution-notifications.md)

---

## How to work through sub-phases

1. **Read** the sub-phase doc and linked SPEC sections
2. **Implement** only what the doc lists
3. **Read your diff** line by line
4. **Run gate tests**
5. **Checkpoint commit**
6. Mark complete before proceeding

**Boundary:** Do not implement chat retention deletion (Phase 07) or admin investigation chat access (Phase 08) in this phase.

---

## Sub-phase index

| # | Doc | Goal | Status |
| - | --- | ---- | ------ |
| 01 | [SUBPHASE-06-01-signalr-contract.md](./SUBPHASE-06-01-signalr-contract.md) | SignalR hub contract + `docs/signalr-contract.md` | Not started |
| 02 | [SUBPHASE-06-02-chat-read-api.md](./SUBPHASE-06-02-chat-read-api.md) | `GET /api/chats`, thread + message history | Not started |
| 03 | [SUBPHASE-06-03-chat-attachment.md](./SUBPHASE-06-03-chat-attachment.md) | Chat photo upload (private) | Not started |
| 04 | [SUBPHASE-06-04-signalr-hub.md](./SUBPHASE-06-04-signalr-hub.md) | Hub: Join, Send, MessageReceived, presence | Not started |
| 05 | [SUBPHASE-06-05-rest-messages.md](./SUBPHASE-06-05-rest-messages.md) | REST message fallback + rate limits (10/min, 60/hr) | Not started |
| 06 | [SUBPHASE-06-06-resolution-api.md](./SUBPHASE-06-06-resolution-api.md) | Confirm resolution + `Resolution` record + irrevocable rules | Not started |
| 07 | [SUBPHASE-06-07-cancel-claim.md](./SUBPHASE-06-07-cancel-claim.md) | Cancel approved claim → `Published`, chat read-only | Not started |
| 08 | [SUBPHASE-06-08-chat-notifications.md](./SUBPHASE-06-08-chat-notifications.md) | Resolution + chat notifications + view suppression | Not started |
| 09 | [SUBPHASE-06-09-chat-ui.md](./SUBPHASE-06-09-chat-ui.md) | My Chats + chat view + safety banner + SignalR client | Not started |
| 10 | [SUBPHASE-06-10-resolution-ui.md](./SUBPHASE-06-10-resolution-ui.md) | Confirm Resolved + Cancel on detail pages | Not started |

---

## Dependencies

- **01**, **02**, **03** → **04**
- **02** → **09**
- **03** → **04**, **05**
- **04** → **05**, **06**, **07**, **08**
- **05**, **06**, **07** → **08**
- **06**, **07** → **10**
- **08** → **09** → **10**

**Rules:** Finish **01–08** (API + hub) before Angular **09–10**.

---

## Mapping to Phase 06 deliverables

| Deliverable | Sub-phase(s) |
| ----------- | ------------ |
| `docs/signalr-contract.md` | 01 |
| `GET /api/chats`, `GET /api/chats/{id}` | 02 |
| `POST /api/uploads/chat-attachment` | 03 |
| `/hubs/chat` SignalR | 04 |
| `POST /api/chats/{id}/messages` + rate limits | 05 |
| `POST confirm-resolution` | 06 |
| `POST cancel` claim | 07 |
| Chat/resolution notifications + suppression | 08 |
| `/my/chats`, `/my/chats/{id}` | 09 |
| `GET /api/uploads/chat-attachment/{id}/url` | 03 |
| Safety banner + SignalR client REST fallback | 09 |
| Pre-signed URL refresh on attachment error | 09 |
| Confirm Resolved / Cancel UI | 10 |
| `Message`, `Resolution`, `readOnlyAt` | 04–07 |
| Section 15.5 + 15.9 | 06–10 |

---

## Phase exit gate

Complete when sub-phase 10 passes and [PHASE-06 acceptance criteria](./PHASE-06-chat-resolution-notifications.md#8-acceptance-criteria) are satisfied.
