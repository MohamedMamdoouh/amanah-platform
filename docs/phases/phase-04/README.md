# Phase 04 — Sub-phases

This directory contains [PHASE-04-browse-discovery.md](./PHASE-04-browse-discovery.md), broken into **8 incremental sub-phases**. Each sub-phase delivers one coherent, testable slice so you can implement, read every line, validate, and test before moving on — no bulk implementation.

**Parent phase:** [PHASE-04 — Browse & Discovery](./PHASE-04-browse-discovery.md)

---

## How to work through sub-phases

For each sub-phase, follow this loop — do not skip steps:

1. **Read** the sub-phase doc and its linked SPEC sections
2. **Implement** only what the doc lists (resist adding extras)
3. **Read your diff** line by line; annotate anything unclear
4. **Run gate tests** listed in the doc
5. **Manual check** the smoke items
6. **Checkpoint commit** (one commit per sub-phase keeps history readable)
7. Mark the sub-phase complete in the progress table below before starting the next

**Local development defaults:**

- Seed at least one `Published` and one `Claim In Progress` report for manual testing (approve in Phase 03 or test fixtures)
- Search column is already populated from Phase 02 — no backfill needed

---

## Dependencies

- **01** → **02** → **03**, **05**
- **04** → **05** (public DTO mapper required before detail API)
- **02**, **04** → **03**, **05**
- **06** → **07**, **08** (static error pages before browse/detail UI)
- **03** → **07**
- **05**, **06**, **07** → **08**

**Rules:**

- Finish sub-phases **01–05** (database + API) before starting Angular **06–08**
- Sub-phase 04 must complete before 05; 02 can run in parallel with 04 but both must finish before 03/05
- Sub-phase 06 (static routes) should complete before browse/detail UI (07, 08) so error pages exist

---

## Sub-phase index

| # | Doc | Goal | Status |
| - | --- | ---- | ------ |
| 01 | [SUBPHASE-04-01-search-index.md](./SUBPHASE-04-01-search-index.md) | `pg_trgm` extension + GIN index on search column | Not started |
| 02 | [SUBPHASE-04-02-browse-query-service.md](./SUBPHASE-04-02-browse-query-service.md) | Search, filters, sort, pagination — service layer only | Not started |
| 03 | [SUBPHASE-04-03-browse-api.md](./SUBPHASE-04-03-browse-api.md) | `GET /api/reports` public browse endpoint | Not started |
| 04 | [SUBPHASE-04-04-public-dto.md](./SUBPHASE-04-04-public-dto.md) | Public DTO mapper — strip private data | Not started |
| 05 | [SUBPHASE-04-05-public-detail-api.md](./SUBPHASE-04-05-public-detail-api.md) | Public detail + status routing + `/lost`/`/found` URLs | Not started |
| 06 | [SUBPHASE-04-06-static-routes.md](./SUBPHASE-04-06-static-routes.md) | `/not-found` and `/unavailable` pages + route config | Not started |
| 07 | [SUBPHASE-04-07-browse-ui.md](./SUBPHASE-04-07-browse-ui.md) | `/browse` search, filters, list, pagination | Not started |
| 08 | [SUBPHASE-04-08-detail-ui.md](./SUBPHASE-04-08-detail-ui.md) | `/lost/{id}`, `/found/{id}` detail + claim CTA stub | Not started |

---

## Mapping to Phase 04 deliverables

| Phase 04 deliverable | Sub-phase(s) |
| -------------------- | ------------ |
| `pg_trgm` GIN index | 01 |
| Arabic normalization search (all-terms AND) | 02, 03 |
| Filters (category, governorate, type, date range) | 02, 03 |
| Sort newest published first; pagination 20/page | 02, 03 |
| `GET /api/reports` | 03 |
| `GET /api/reports/{id}/public` | 05 |
| `/lost/{id}`, `/found/{id}` API handlers | 05 |
| Strip private photos, hidden detail, phone | 04, 05 |
| `Claim In Progress` label | 04, 08 |
| Claim CTA + message stub (login prompt) | 08 |
| My Reports Published tab → public URL | 08 (+ Phase 03 sub-10 link) |
| `/browse` | 07 |
| `/not-found`, `/unavailable` | 06, 08 |
| URL status routing | 05, 08 |
| Section 15.3 acceptance criteria | 02–05 (API), 06–08 (E2E) |

---

## Phase exit gate

Phase 04 is complete when sub-phase 08 passes and all acceptance criteria in [PHASE-04-browse-discovery.md](./PHASE-04-browse-discovery.md#8-acceptance-criteria) are satisfied.
