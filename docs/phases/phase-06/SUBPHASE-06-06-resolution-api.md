# Sub-phase 06 — Resolution API

**Status:** Not started  
**Prerequisites:** [Sub-phase 04](./SUBPHASE-06-04-signalr-hub.md)

---

## 1. Summary

`POST /api/claims/{id}/confirm-resolution` — mutual confirm flow. Creates/updates `Resolution` record. Second confirm → report `Resolved`, chat read-only.

---

## 2. Deliverables

### Flow

1. Caller must be reporter or approved claimant on `Approved` claim / `Claim In Progress` report
2. Set `reporterConfirmedAt` or `claimantConfirmedAt` (idempotent if already confirmed)
3. **Irrevocable:** once confirmed, that party cannot cancel (sub-phase 07 enforces)
4. First confirm → notify counterparty `CounterpartyConfirmedResolution`
5. Both confirmed → `Resolution.resolvedAt`, report → `Resolved`, `ChatThread.readOnlyAt`, notify both `ReportResolved`, broadcast `ResolutionUpdated` and `ThreadReadOnly` (same as cancel path in sub-phase 07)

### `POST` refused when

- Claim not `Approved` / report not `Claim In Progress`
- Party already confirmed (return `200` idempotent OK)

---

## 3. Exit criteria

- [ ] First confirm: CIP status unchanged, notification sent
- [ ] Second confirm: `Resolved`, chat read-only, `ThreadReadOnly` broadcast to hub group
- [ ] Confirmer cannot cancel afterward (test with sub-phase 07)
- [ ] Mark complete in [phase-06/README.md](./README.md)
