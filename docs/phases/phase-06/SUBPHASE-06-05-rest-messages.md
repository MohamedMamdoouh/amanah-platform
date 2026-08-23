# Sub-phase 05 — REST Messages & Rate Limits

**Status:** Not started  
**Prerequisites:** [Sub-phases 03](./SUBPHASE-06-03-chat-attachment.md), [04](./SUBPHASE-06-04-signalr-hub.md)

---

## 1. Summary

`POST /api/chats/{threadId}/messages` as REST fallback (same validation as hub). Chat rate limits: **10/min, 60/hour** per account (Section 7.5).

---

## 2. Deliverables

Reuse `ChatMessageService` from hub logic — single code path for send.

| Limit | Error |
| ----- | ----- |
| 10 / minute | `chat.rate_limit_minute` |
| 60 / hour | `chat.rate_limit_hour` |

Return `429` + `Retry-After`.

---

## 3. Exit criteria

- [ ] REST send works when SignalR disconnected
- [ ] 11th message in minute → `429`
- [ ] Mark complete in [phase-06/README.md](./README.md)
