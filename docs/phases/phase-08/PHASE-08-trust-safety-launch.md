# Phase 08 — Trust, Safety & Launch Readiness

**Status:** Not started  
**Prerequisites:** Phase 07 — Lifecycle, Retention & Account Management

**Incremental implementation:** This phase is broken into 11 sub-phases for step-by-step work. Start at [README.md](./README.md).

---

## 1. Summary

Complete v1 with abuse reporting, admin enforcement (takedown, ban, unban), admin access to chat/claim content during flagged-listing investigations, and a full permissions-matrix audit. Run an end-to-end acceptance pass against all Section 15 criteria, verify all Section 7.5 rate limits, confirm Section 10 out-of-scope items remain excluded, and resolve the domain name before launch.

---

## 2. SPEC references

| SPEC section | Topic |
| ------------ | ----- |
| Section 7 | Abuse, safety, and enforcement (entire section) |
| Section 8 | Enforcement-related status transitions |
| Section 5.5 | Abuse queue, user lookup, ban/unban |
| Section 9 | Full permissions matrix enforcement |
| Section 10 | Out of scope confirmation |
| Section 11 | NFR final pass (accessibility, browser support) |
| Section 13 | Risks — verify mitigations are in place |
| Section 15 | Full acceptance criteria regression |
| Section 7.5 | All rate limits verification |

---

## 3. Prerequisites

### Prior phases

- [ ] Phases 01–07 complete

### Deferred decisions (Section 14)

Resolve **before starting** this phase:

| Item | Notes |
| ---- | ----- |
| Domain name | Configure custom domain on Railway before launch — see [Sub-phase 01](./SUBPHASE-08-01-decisions.md) |

---

## 4. Deliverables

### API

| Method | Route | Purpose |
| ------ | ----- | ------- |
| POST | `/api/reports/{id}/flag` | Flag a publicly visible listing |
| GET | `/api/reports/{id}/flag` | Get user's open flag on listing (if any) |
| GET | `/api/admin/abuse` | Abuse report queue |
| GET | `/api/admin/abuse/{id}` | Abuse report detail (flag reason, listing summary — **not** inline chat/claim content) |
| POST | `/api/admin/abuse/{id}/resolve` | Resolve: no action / takedown / ban |
| POST | `/api/admin/reports/{id}/takedown` | Take down published report |
| GET | `/api/admin/users` | User lookup (by display name or phone) |
| GET | `/api/admin/users/{id}` | User detail (`reportsCount`, ban status) |
| POST | `/api/admin/users/{id}/ban` | Ban user with reason |
| POST | `/api/admin/users/{id}/unban` | Unban user |
| GET | `/api/admin/investigations/{reportId}/chat` | Chat content during flagged-listing investigation |
| GET | `/api/admin/investigations/{reportId}/claims` | Claim text/photos during investigation |
| GET | `/api/admin/investigations/{reportId}/photos` | Private report photos during investigation |

### UI routes

| Route | Access | Purpose |
| ----- | ------ | ------- |
| `/lost/{id}` / `/found/{id}` | Logged-in | Flag listing button |
| `/my/chats/{threadId}` | Logged-in | Report shortcut in chat header |
| `/admin/abuse` | Admin | Abuse report queue |
| `/admin/abuse/{id}` | Admin | Investigation and resolve |
| `/admin/users` | Admin | User lookup |
| `/admin/users/{id}` | Admin | Ban/unban actions |

### Database

- `AbuseReport` with status `Open` → `Resolved`, resolution outcome
- Reuse Phase 01 `User.IsBanned`, `User.BanReason`; add `User.BannedAt` if not present
- Enforcement calls `ReportLifecycleService`, `ClaimCleanupService` from Phase 07

### Infrastructure

- Custom domain configured on Railway
- Final production environment review (secrets, CORS, HTTPS)

### Shared utilities

- Flag reason enum (5 predefined reasons per Section 7.1)
- One open flag per user per listing enforcement
- Ban side effects (Section 7.2): sign out everywhere, withdraw reports, cancel claims, notify counterparties
- Unban: restore sign-in only; no restoration of withdrawn/cancelled content
- Admin investigation mode: temporary access to chat/claim content for flagged listings only

---

## 5. Permissions (Section 9)

Full matrix audit — every row must be server-enforced. See sub-phase 10.

---

## 6. Notifications (Section 5.7)

| Event | Recipient | Introduced |
| ----- | --------- | ---------- |
| Admin takedown affecting you | Reporter and approved claimant | this phase |
| Claim ended by enforcement | Affected party | this phase |
| Abuse report resolved | Flagger | this phase |

All prior notification types from Phases 03–07 must still pass regression.

---

## 7. Out of scope

Confirm Section 10 items remain **not implemented** — verified in [Sub-phase 11](./SUBPHASE-08-11-launch-readiness.md).

---

## 8. Acceptance criteria

From [SPEC.md Section 15.6](../../SPEC.md#156-abuse-and-enforcement).

- [ ] **Flagging constraints:** a listing owner cannot flag their own listing, each user can have at most one open flag per listing, a duplicate flag is refused, and the reason must come from the predefined list
- [ ] **Abuse workflow:** an abuse report moves `Open` → `Resolved` with an outcome of no action taken, report taken down, or user banned, and the flagger is notified of the high-level outcome
- [ ] **Ban cleanup:** on ban the user is signed out everywhere, their `Pending Review` and `Published` reports are withdrawn, their pending claims are withdrawn, any approved claim they are part of is cancelled, a report of theirs in `Claim In Progress` ends as `Withdrawn`, impacted counterparties are notified, and a later sign-in attempt is refused with the ban reason
- [ ] **Unban:** an unbanned user can sign in again, and nothing withdrawn or cancelled by the ban is restored
- [ ] **Takedown during `Claim In Progress`:** the approved claim is cancelled first, the report becomes `Removed by Admin`, and the chat becomes read-only immediately
- [ ] **In-chat report shortcut:** a user in a chat thread can open the listing-flag flow for the linked report; if they already have an open flag, the UI shows that flag instead of creating a duplicate

From [SPEC.md Section 15.8](../../SPEC.md#158-privacy-and-permissions).

- [ ] **Private photos (`photosPrivate`):** never exposed on public listing or to claimants — reporter and admin only
- [ ] **Hidden verification detail:** reporter only — never claimant, public, or admin
- [ ] **Claim text and photos:** claimant + reporter; admin only during flagged-listing investigation
- [ ] **Chat access:** two parties only; admin during flagged-listing investigation
- [ ] **Phone numbers:** never returned to another user
- [ ] **Role enforcement:** every row of Section 9 enforced server-side

Full Section 15.1–15.9 regression: [Sub-phase 11](./SUBPHASE-08-11-launch-readiness.md).

---

## 9. Definition of done

See sub-phases 10–11 for automated permission tests, rate limit audit, Section 15 regression, and launch checklist.

### Phase exit gate

This phase is complete when all acceptance criteria pass, the launch checklist is signed off, and v1 is deployable.
