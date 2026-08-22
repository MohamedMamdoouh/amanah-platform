# Phase 08 — Trust, Safety & Launch Readiness

**Status:** Not started  
**Prerequisites:** Phase 07 — Lifecycle, Retention & Account Management

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
| Domain name | Configure custom domain on Railway before launch |

---

## 4. Deliverables

### API

| Method | Route | Purpose |
| ------ | ----- | ------- |
| POST | `/api/reports/{id}/flag` | Flag a publicly visible listing |
| GET | `/api/reports/{id}/flag` | Get user's open flag on listing (if any) |
| GET | `/api/admin/abuse` | Abuse report queue |
| GET | `/api/admin/abuse/{id}` | Abuse report detail (incl. chat/claim access) |
| POST | `/api/admin/abuse/{id}/resolve` | Resolve: no action / takedown / ban |
| POST | `/api/admin/reports/{id}/takedown` | Take down published report |
| GET | `/api/admin/users` | User lookup (by display name or phone) |
| GET | `/api/admin/users/{id}` | User detail |
| POST | `/api/admin/users/{id}/ban` | Ban user with reason |
| POST | `/api/admin/users/{id}/unban` | Unban user |
| GET | `/api/admin/investigations/{reportId}/chat` | Chat content during flagged-listing investigation |
| GET | `/api/admin/investigations/{reportId}/claims` | Claim text/photos during investigation |

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
- Ban fields on `User`: `banned`, `banReason`, `bannedAt`
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

Full matrix audit — every row must be server-enforced:

| Data | Public | Logged-in | Pending claimant | Approved claimant | Reporter | Admin |
| ---- | ------ | --------- | ---------------- | ----------------- | -------- | ----- |
| Title, description, category fields | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ |
| Public photos | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ |
| Private photos | — | — | — | — | ✓ | ✓ (review/enforcement) |
| Hidden verification detail | — | — | — | — | ✓ | **never** |
| Reward, held location | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ |
| Display name of reporter | ✓ | ✓ | ✓ | ✓ | own | ✓ |
| Display name of claimant | — | — | own | own | ✓ | ✓ |
| Phone numbers | — | own | own | own | own | ✓ |
| Claim text and photo | — | — | own | own | ✓ | investigation only |
| Chat thread | — | — | — | ✓ | ✓ | investigation only |
| Withdrawal reason | — | — | — | — | own | ✓ |

Run automated permission tests for every cell.

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

Confirm these Section 10 items remain **not implemented**:

- Automated/AI matching
- Claim rejection appeals
- Editing published report content
- In-chat block user
- Per-photo visibility control
- Resolution disputes/reopening
- Display-name edits, phone change, Terms re-acceptance
- Notification preferences, data export
- Automated profanity filtering
- Social link previews
- In-app payments/escrow
- Native mobile apps (PWA only)
- English localization
- Community moderation
- Monetization
- Web push / SMS event notifications
- Map/GPS location
- Draft report saving
- Formal performance targets

---

## 8. Acceptance criteria

From [SPEC.md Section 15.6](../SPEC.md#156-abuse-and-enforcement).

- [ ] **Flagging constraints:** a listing owner cannot flag their own listing, each user can have at most one open flag per listing, a duplicate flag is refused, and the reason must come from the predefined list
- [ ] **Abuse workflow:** an abuse report moves `Open` → `Resolved` with an outcome of no action taken, report taken down, or user banned, and the flagger is notified of the high-level outcome
- [ ] **Ban cleanup:** on ban the user is signed out everywhere, their `Pending Review` and `Published` reports are withdrawn, their pending claims are withdrawn, any approved claim they are part of is cancelled, a report of theirs in `Claim In Progress` ends as `Withdrawn`, impacted counterparties are notified, and a later sign-in attempt is refused with the ban reason
- [ ] **Unban:** an unbanned user can sign in again, and nothing withdrawn or cancelled by the ban is restored
- [ ] **Takedown during `Claim In Progress`:** the approved claim is cancelled first, the report becomes `Removed by Admin`, and the chat becomes read-only immediately
- [ ] **In-chat report shortcut:** a user in a chat thread can open the listing-flag flow for the linked report; if they already have an open flag, the UI shows that flag instead of creating a duplicate

From [SPEC.md Section 15.8](../SPEC.md#158-privacy-and-permissions).

- [ ] **Private photos (`photosPrivate`):** categories with `photosPrivate` never expose photos on a public listing or to claimants — only the reporter and admin (during review/enforcement)
- [ ] **Hidden verification detail:** returned only to its reporter, never to claimant, public, or admin
- [ ] **Claim text and photos:** visible to own claimant and reporter; admin only during flagged-listing investigation
- [ ] **Chat access:** only two parties; admin during flagged-listing investigation
- [ ] **Phone numbers:** never returned to another user
- [ ] **Role enforcement:** every row of Section 9 enforced server-side

### Full Section 15 regression checklist

Run all criteria from Section 15.1–Section 15.9. Any failures block launch.

| Section | Phase introduced | Regression |
| ------- | ---------------- | ---------- |
| Section 15.1 | Phase 02 | required |
| Section 15.2 | Phase 03 + 07 | required |
| Section 15.3 | Phase 04 | required |
| Section 15.4 | Phase 05 + 07 | required |
| Section 15.5 | Phase 06 + 07 | required |
| Section 15.6 | Phase 08 | required |
| Section 15.7 | Phase 01 | required |
| Section 15.8 | Phase 08 | required |
| Section 15.9 | Phase 06 | required |

### Rate limits (Section 7.5)

- [ ] 3 new reports/day per account
- [ ] 5 claim submissions/day per account
- [ ] Max 5 open reports per account
- [ ] Photo uploads: 5/min, 20/hour
- [ ] Chat messages: 10/min, 60/hour
- [ ] OTP send limits (Phase 01)
- [ ] All return HTTP 429 with `Retry-After`

---

## 9. Definition of done

### Automated tests

- [ ] Flag listing: constraints, duplicate refused, predefined reasons
- [ ] Abuse workflow: Open → Resolved with all three outcomes
- [ ] Takedown: claim cancelled first, chat read-only
- [ ] Ban: all side effects; sign-in refused with reason
- [ ] Unban: sign-in restored; content not restored
- [ ] Admin investigation: chat/claim access only for flagged listings
- [ ] Hidden detail never in any admin API response
- [ ] Full Section 9 permission matrix tests
- [ ] All Section 7.5 rate limits
- [ ] Full Section 15 regression suite

### Manual smoke checklist

- [ ] Flag a listing from browse; flag from chat header
- [ ] Admin resolves abuse report with each outcome
- [ ] Ban user; verify cleanup; unban; verify no restoration
- [ ] Takedown report in `Claim In Progress`
- [ ] Custom domain serves HTTPS
- [ ] Keyboard navigation sanity on browse, submit, claim, chat
- [ ] WCAG 2.1 AA goal: spot-check core flows
- [ ] Walk through Section 13 risk table; confirm each mitigation is observable

### Launch checklist

- [ ] Domain configured
- [ ] SMS provider production credentials
- [ ] Email provider production credentials
- [ ] Admin phone bootstrapped
- [ ] Railway backups enabled
- [ ] Privacy Policy discloses cross-border hosting and PDPL gaps (Section 5.8)
- [ ] All Section 10 out-of-scope items confirmed absent
- [ ] Full Section 15 regression pass

### Phase exit gate

This phase is complete when all acceptance criteria pass, the launch checklist is signed off, and v1 is deployable.
