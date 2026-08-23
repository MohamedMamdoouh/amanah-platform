# Phase 08 — Sub-phases

This directory contains [PHASE-08-trust-safety-launch.md](./PHASE-08-trust-safety-launch.md), broken into **11 incremental sub-phases**. Sub-phases 01–09 build features; **10–11** are audit and launch.

**Parent phase:** [PHASE-08 — Trust, Safety & Launch Readiness](./PHASE-08-trust-safety-launch.md)

---

## How to work through sub-phases

1. **Resolve domain** in sub-phase 01 before production deploy (sub-phase 11)
2. **Use Phase 07 services** for takedown/ban — never duplicate cleanup logic
3. **Do not skip** sub-phases 10–11 — they are the v1 exit gate

---

## Sub-phase index

| # | Doc | Goal | Status |
| - | --- | ---- | ------ |
| 01 | [SUBPHASE-08-01-decisions.md](./SUBPHASE-08-01-decisions.md) | Flag reasons, abuse outcomes, domain decision | Not started |
| 02 | [SUBPHASE-08-02-flag-api.md](./SUBPHASE-08-02-flag-api.md) | `POST/GET /api/reports/{id}/flag` | Not started |
| 03 | [SUBPHASE-08-03-flag-ui.md](./SUBPHASE-08-03-flag-ui.md) | Flag from detail pages + chat header | Not started |
| 04 | [SUBPHASE-08-04-abuse-admin-api.md](./SUBPHASE-08-04-abuse-admin-api.md) | Abuse queue, detail, resolve | Not started |
| 05 | [SUBPHASE-08-05-takedown-api.md](./SUBPHASE-08-05-takedown-api.md) | Admin takedown (uses Phase 07 cleanup) | Not started |
| 06 | [SUBPHASE-08-06-ban-unban-api.md](./SUBPHASE-08-06-ban-unban-api.md) | User lookup, ban, unban | Not started |
| 07 | [SUBPHASE-08-07-investigation-api.md](./SUBPHASE-08-07-investigation-api.md) | Admin chat/claim access for flagged listings | Not started |
| 08 | [SUBPHASE-08-08-enforcement-ui.md](./SUBPHASE-08-08-enforcement-ui.md) | `/admin/abuse`, `/admin/users` | Not started |
| 09 | [SUBPHASE-08-09-enforcement-notifications.md](./SUBPHASE-08-09-enforcement-notifications.md) | Takedown, claim-ended, abuse-resolved events | Not started |
| 10 | [SUBPHASE-08-10-permissions-audit.md](./SUBPHASE-08-10-permissions-audit.md) | Full Section 9 matrix automated tests | Not started |
| 11 | [SUBPHASE-08-11-launch-readiness.md](./SUBPHASE-08-11-launch-readiness.md) | Rate limits, Section 15 regression, domain, launch checklist | Not started |

---

## Dependencies

- **01** → **02** → **03**, **04**
- **04** → **07**, **08**
- **05**, **06**, **07** → **08**
- **04**, **05**, **06** → **09** (sub-phase 09 = regression suite; sub-phases 04–06 fire notifications inline via `INotificationService`)
- **09** → **10** → **11**

**Rules:** Finish **01–09** (features) before **10–11** (audit + launch). Phase 07 cleanup services must exist before **05** and **06**.

---

## Mapping to Phase 08 deliverables

| Deliverable | Sub-phase(s) |
| ----------- | ------------ |
| Flag reason enum + flag API | 01, 02 |
| `AbuseReport` + `User` ban fields migration | 01 |
| Flag UI (browse + chat) | 03 |
| Abuse queue/resolve API | 04 |
| Takedown API | 05 |
| User lookup + ban/unban | 06 |
| Investigation chat/claims/photos API | 07 |
| Admin abuse + users UI | 08 |
| Enforcement notifications | 09 |
| Section 9 full audit | 10 |
| Section 7.5 rate limits + Section 15 regression | 11 |
| Domain + production config | 01, 11 |
| Section 10 out-of-scope verification | 11 |
| Section 13 risk verification | 11 |

---

## Phase exit gate

v1 deployable when sub-phase 11 launch checklist is signed off.
