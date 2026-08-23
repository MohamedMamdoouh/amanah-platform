# Sub-phase 10 — Resolution UI

**Status:** Not started  
**Prerequisites:** [Sub-phases 06–07](./SUBPHASE-06-06-resolution-api.md), [09](./SUBPHASE-06-09-chat-ui.md)

---

## 1. Summary

Confirm Resolved and Cancel Claim buttons on report detail and/or chat header for approved-claim participants. Completes Phase 06.

---

## 2. Deliverables

### On `/lost/{id}` / `/found/{id}` (logged-in participant)

- **Confirm Resolved** — calls `POST /api/claims/{id}/confirm-resolution`
- **Cancel claim** — confirm dialog → `POST /api/claims/{id}/cancel`
- Hide after `Resolved` or when user already confirmed (show awaiting counterparty state)
- Disable cancel after own confirm

### UX copy

- First confirm: "في انتظار تأكيد الطرف الآخر"
- Both confirmed: redirect or success state; public URL → unavailable (Phase 04)

---

## 3. Exit criteria

### Phase 06 final gate

Re-run [PHASE-06 definition of done](./PHASE-06-chat-resolution-notifications.md#9-definition-of-done).

- [ ] Mutual resolution E2E
- [ ] Cancel returns report to claimable
- [ ] Mark sub-phase 10 and Phase 06 complete in [phase-06/README.md](./README.md)
