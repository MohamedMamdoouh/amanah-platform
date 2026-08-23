# Sub-phase 07 — Cancel Approved Claim

**Status:** Not started  
**Prerequisites:** [Sub-phase 06](./SUBPHASE-06-06-resolution-api.md)

---

## 1. Summary

`POST /api/claims/{id}/cancel` — either party cancels before mutual confirm (with irrevocable rules). Claim → `Cancelled`, report → `Published`, chat read-only, `countsAsAttempt = true` if claimant-initiated cancel of approved claim per SPEC 6.4.

---

## 2. Deliverables

### Rules

- Allowed while claim `Approved` and neither party has confirmed **OR** only non-confirmer can cancel after other confirmed (SPEC 4.6.4)
- Party who already confirmed → `409 claim.cannot_cancel_after_confirm`
- Set `ChatThread.readOnlyAt = now`
- Notify counterparty: `ClaimCancelledByCounterparty`
- Broadcast `ThreadReadOnly` via SignalR

### Timer note

Report published timer resumes (Phase 07) — add TODO hook for `ReportLifecycleService`.

---

## 3. Exit criteria

- [ ] Cancel → `Published`, chat read-only
- [ ] Post-confirm cancel blocked for confirmer
- [ ] Attempt counted when applicable
- [ ] Mark complete in [phase-06/README.md](./README.md)
