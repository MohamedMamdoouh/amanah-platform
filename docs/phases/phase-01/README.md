# Phase 01 — Sub-phases

This directory contains [PHASE-01-platform-foundation.md](./PHASE-01-platform-foundation.md), broken into **12 incremental sub-phases**. Each sub-phase delivers one coherent, testable slice so you can implement, read every line, validate, and test before moving on — no bulk implementation.

**Parent phase:** [PHASE-01 — Platform Foundation](./PHASE-01-platform-foundation.md)

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

- SMS: `ConsoleSmsSender` logs OTP codes to the console (sub-phases 06–11)
- Real SMS provider: wired only in sub-phase 12 (Railway deploy)

---

## Dependencies

- **01** → **02** → **03** → **04** → **05** → **06** → **07** → **08** → **09** → **10** → **11** → **12**
- **08** also required before **11** (sessions before auth UI)

Sub-phases 09–11 can overlap slightly (schema/seeds while polishing UI), but **do not start 12 until 11 passes locally**.

---

## Sub-phase index

| # | Doc | Goal | Status |
| - | --- | ---- | ------ |
| 01 | [SUBPHASE-01-01-decisions.md](./SUBPHASE-01-01-decisions.md) | Resolve Section 14 blockers; API error contract; service interfaces | Not started |
| 02 | [SUBPHASE-01-02-scaffold.md](./SUBPHASE-01-02-scaffold.md) | Runnable monorepo skeleton (API + Angular + local Postgres) | Not started |
| 03 | [SUBPHASE-01-03-api-plumbing.md](./SUBPHASE-01-03-api-plumbing.md) | Error contract mapper, rate-limit middleware, test harness | Not started |
| 04 | [SUBPHASE-01-04-auth-db.md](./SUBPHASE-01-04-auth-db.md) | EF Core auth entities and first migration | Not started |
| 05 | [SUBPHASE-01-05-utilities.md](./SUBPHASE-01-05-utilities.md) | Cairo timezone, text normalization, Arabic search normalization | Not started |
| 06 | [SUBPHASE-01-06-otp-send.md](./SUBPHASE-01-06-otp-send.md) | `POST /api/auth/otp/send` with limits and bot check | Not started |
| 07 | [SUBPHASE-01-07-otp-verify.md](./SUBPHASE-01-07-otp-verify.md) | `POST /api/auth/otp/verify` with attempt limits | Not started |
| 08 | [SUBPHASE-01-08-sessions.md](./SUBPHASE-01-08-sessions.md) | Register, login, JWT, refresh, logout, `/me` | Not started |
| 09 | [SUBPHASE-01-09-schema-seeds.md](./SUBPHASE-01-09-schema-seeds.md) | Full Section 17 schema, seeds, admin bootstrap | Not started |
| 10 | [SUBPHASE-01-10-angular-shell.md](./SUBPHASE-01-10-angular-shell.md) | RTL shell, static pages, PWA manifest | Not started |
| 11 | [SUBPHASE-01-11-auth-ui-admin.md](./SUBPHASE-01-11-auth-ui-admin.md) | Login flow, auth state, admin role guard | Not started |
| 12 | [SUBPHASE-01-12-railway-deploy.md](./SUBPHASE-01-12-railway-deploy.md) | Railway deploy, real SMS, production verification | Not started |

---

## Mapping to Phase 01 deliverables

| Phase 01 deliverable | Sub-phase(s) |
| -------------------- | ------------ |
| API error contract | 01 |
| Monorepo + Railway stack | 02, 12 |
| Auth endpoints (8 routes) | 06, 07, 08 |
| UI routes (7 routes) | 10, 11 |
| Auth DB entities | 04 |
| Full DB schema + seeds | 09 |
| Shared utilities | 05 |
| Rate limit 429 pattern | 03 (+ OTP limits in 06) |
| JWT + roles | 08 |
| PWA + RTL | 10 |
| Permissions (phone, display name) | 08, 11 |
| Section 15.7 acceptance criteria | 06–08 (API tests), 12 (full E2E) |

---

## Phase exit gate

Phase 01 is complete when sub-phase 12 passes and all acceptance criteria in [PHASE-01-platform-foundation.md](./PHASE-01-platform-foundation.md#8-acceptance-criteria) are satisfied on the deployed environment.
