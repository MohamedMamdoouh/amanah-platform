# Sub-phase 03 — Chat Attachment Upload

**Status:** Not started  
**Prerequisites:** Phase 05 claim photo upload pattern, [Sub-phase 02](./SUBPHASE-06-02-chat-read-api.md)

---

## 1. Summary

`POST /api/uploads/chat-attachment` — private photo for chat messages. Same rules as claim photos (5 MB, EXIF strip, WebP optional). Linked to message on send.

---

## 2. Deliverables

| Method | Route | Auth |
| ------ | ----- | ---- |
| POST | `/api/uploads/chat-attachment` | Participant in active (non-read-only) thread |
| GET | `/api/uploads/chat-attachment/{id}/url` | Thread participants only |

Orphan attachment until message sent; TTL cleanup stub OK.

---

## 3. Exit criteria

- [ ] Upload + presigned URL for participants only
- [ ] Mark complete in [phase-06/README.md](./README.md)
