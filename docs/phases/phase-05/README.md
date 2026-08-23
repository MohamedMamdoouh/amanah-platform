# Phase 05 — Sub-phases

This directory contains [PHASE-05-claims-verification.md](./PHASE-05-claims-verification.md), broken into **9 incremental sub-phases**. Each sub-phase delivers one coherent, testable slice so you can implement, read every line, validate, and test before moving on — no bulk implementation.

**Parent phase:** [PHASE-05 — Claims & Verification](./PHASE-05-claims-verification.md)

---

## How to work through sub-phases

1. **Read** the sub-phase doc and linked SPEC sections
2. **Implement** only what the doc lists
3. **Read your diff** line by line
4. **Run gate tests**
5. **Manual smoke check**
6. **Checkpoint commit**
7. Mark complete in the progress table before proceeding

**Phase 05 / 06 boundary:** Sub-phase 05 creates `ChatThread` rows only — no SignalR, no messages, no chat UI.

---

## Dependencies

- **01** → **02** → **04**
- **03** → **04**
- **04** → **05**, **06**
- **05** → **06**
- **04**, **06** → **07** (claim form UI)
- **05**, **06** → **08** (my claims / reporter review UI)
- **06** → **09** (`ClaimCleanupService`; does not block UI)

**Rules:** Finish **01–06** (core claim API) before Angular **07–08** (claim form before reporter/My Claims UI). Complete **09** before marking Phase 05 done — it can run after **07–08** smoke tests. Sub-phase 09 builds `ClaimCleanupService`; E2E pending-claim closure on reporter withdraw ships in Phase 07.

---

## Sub-phase index

Work order **#** is the sequence you implement. **File** is the doc filename (may differ when UI order differs from numeric filename).

| # | File | Doc | Goal | Status |
| - | ---- | --- | ---- | ------ |
| 01 | 05-01 | [SUBPHASE-05-01-decisions.md](./SUBPHASE-05-01-decisions.md) | Claim statuses, attempt rules, notification types | Not started |
| 02 | 05-02 | [SUBPHASE-05-02-validation-quota.md](./SUBPHASE-05-02-validation-quota.md) | Claim text validation, contact-info block, daily quota (5/day) | Not started |
| 03 | 05-03 | [SUBPHASE-05-03-claim-photo.md](./SUBPHASE-05-03-claim-photo.md) | Private claim photo upload + pre-signed URLs | Not started |
| 04 | 05-04 | [SUBPHASE-05-04-claim-create.md](./SUBPHASE-05-04-claim-create.md) | `POST /api/reports/{id}/claims` + `NewClaimSubmitted` notification | Not started |
| 05 | 05-05 | [SUBPHASE-05-05-approve-reject.md](./SUBPHASE-05-05-approve-reject.md) | Approve/reject, `ChatThread` stub, auto-reject others, `GET` claims on report | Not started |
| 06 | 05-06 | [SUBPHASE-05-06-withdraw-read.md](./SUBPHASE-05-06-withdraw-read.md) | Withdraw, `GET /api/claims/mine`, detail | Not started |
| 07 | 05-08 | [SUBPHASE-05-08-claim-form-ui.md](./SUBPHASE-05-08-claim-form-ui.md) | Claim form on `/lost/{id}` and `/found/{id}` | Not started |
| 08 | 05-07 | [SUBPHASE-05-07-my-claims-ui.md](./SUBPHASE-05-07-my-claims-ui.md) | My Claims, reporter claims section, Claim In Progress tab | Not started |
| 09 | 05-09 | [SUBPHASE-05-09-withdraw-closure.md](./SUBPHASE-05-09-withdraw-closure.md) | `ClaimCleanupService` + timeout env stub (E2E closure in Phase 07) | Not started |

---

## Mapping to Phase 05 deliverables

| Phase 05 deliverable | Sub-phase(s) |
| -------------------- | ------------ |
| Claim domain types + attempt rules | 01 |
| Contact-info block on claim text | 02, 04 |
| Daily claim quota (5/day) | 02, 04 |
| `POST /api/uploads/claim-photo` + URL | 03 |
| `POST /api/reports/{id}/claims` | 04 |
| `POST approve` / `POST reject` / `GET /api/reports/{id}/claims` | 05 |
| `ChatThread` placeholder on approve | 05 |
| Auto-reject `Another claim approved` | 05 |
| `POST withdraw`, `GET mine`, `GET detail` | 06 |
| Direction-specific claim prompt copy | 07, 08 |
| All claim notifications (except auto-withdraw, cancel) | 04, 05, 06, 09 |
| `ClaimCleanupService` (unit-tested; E2E closure in Phase 07) | 09 |
| `CLAIM_TIMEOUT_MINUTES` + optional admin timeout trigger stub | 09 |
| Claim form on detail pages | 07 |
| My Claims + reporter review UI + Claim In Progress tab | 08 |
| Admin claim-text access stub (`404` until Phase 08) | 06 |
| Section 15.4 (except 10-day job and E2E pending-claim closure) | 02–07, 08–09 |

---

## Phase exit gate

Phase 05 is complete when sub-phases 07–09 pass and all acceptance criteria in [PHASE-05-claims-verification.md](./PHASE-05-claims-verification.md#8-acceptance-criteria) are satisfied.
