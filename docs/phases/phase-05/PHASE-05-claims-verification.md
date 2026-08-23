# Phase 05 — Claims & Verification

**Status:** Not started  
**Prerequisites:** Phase 04 — Browse & Discovery

**Incremental implementation:** This phase is broken into 9 sub-phases for step-by-step work. Start at [README.md](./README.md).

---

## 1. Summary

Implement the full claim lifecycle: logged-in users submit claims on `Published` reports, reporters manually approve or reject, attempt limits apply per user per report, and approving a claim moves the report to `Claim In Progress` and creates a chat thread record. Real-time messaging is deferred to Phase 06 — the thread exists but is inert until then. In-app notifications for all claim events (except chat and resolution) are added.

---

## 2. SPEC references

| SPEC section | Topic |
| ------------ | ----- |
| Section 4.5 | Claiming / verification (reference to Section 6) |
| Section 4.8 | My Claims; report-detail Claims section |
| Section 6 | Verification and claim model (entire section) |
| Section 7.5 | Daily claim quota |
| Section 8 | `Published` ↔ `Claim In Progress` transitions |
| Section 9 | Claimant/reporter claim visibility |
| Section 15.4 | Claiming acceptance criteria (except 10-day timeout job) |
| Section 19 | Claim photo upload (private, pre-signed) |

**Part II (technical):** Section 19 (claim photo)

---

## 3. Prerequisites

### Prior phases

- [ ] Phase 01 — Platform Foundation
- [ ] Phase 02 — Report Submission
- [ ] Phase 03 — Admin Moderation
- [ ] Phase 04 — Browse & Discovery

### Deferred decisions (Section 14)

None additional.

---

## 4. Deliverables

### API

| Method | Route | Purpose |
| ------ | ----- | ------- |
| POST | `/api/reports/{id}/claims` | Submit claim on `Published` report |
| GET | `/api/reports/{id}/claims` | Reporter: list claims on own report |
| POST | `/api/claims/{id}/approve` | Reporter approves → `Claim In Progress` |
| POST | `/api/claims/{id}/reject` | Reporter rejects claim |
| POST | `/api/claims/{id}/withdraw` | Claimant withdraws `Pending` claim |
| GET | `/api/claims/mine` | Claimant's claims (My Claims) |
| GET | `/api/claims/{id}` | Claim detail (claimant or reporter) |
| POST | `/api/uploads/claim-photo` | Upload claim photo (private bucket) |
| GET | `/api/uploads/claim-photo/{id}/url` | Pre-signed URL (claimant, reporter, admin on investigation) |

### UI routes

| Route | Access | Purpose |
| ----- | ------ | ------- |
| `/lost/{id}` / `/found/{id}` | Logged-in | Claim submission form on `Published` reports |
| `/my/claims` | Logged-in | My Claims list |
| `/my/reports/{id}` | Logged-in (reporter) | Claims section with approve/reject |
| `/my/reports` | Logged-in | Claim In Progress tab |

### Database

- `Claim` records with status, attempt tracking, `countsAsAttempt` flag
- `ChatThread` record created on claim approval (messaging inert until Phase 06)
- `Report.status` → `Claim In Progress` on approval
- Auto-reject other `Pending` claims with reason `Another claim approved`

### Infrastructure

- Private bucket for claim photos
- Pre-signed URLs (5-minute expiry) for claim photo access

### Shared utilities

- Direction-specific claim prompt copy (lost vs found)
- Contact-info block on claim text
- Attempt counter: manual rejection and claimant-initiated cancellation of approved claim count; others do not
- Daily claim quota: 5 submissions/day (Africa/Cairo)
- One open `Pending` claim per user per report
- 3 attempts per user per report lifetime

### Phase 05 / Phase 06 boundary

> **Important:** On claim approval, a `ChatThread` row is created and linked to the claim, but no SignalR hub, message endpoints, or chat UI are wired. The thread is a placeholder. Phase 06 activates messaging on existing threads. Document this in code comments and QA checklists to avoid scope creep.

---

## 5. Permissions (Section 9)

Server-enforce these matrix rows before marking this phase done:

| Data | Claimant | Reporter | Admin |
| ---- | -------- | -------- | ----- |
| Claim text and claim photo | own | ✓ (for review) | ✓ (flagged-listing investigation only — stub OK; full in Phase 08) |
| Display name of claimant | own | ✓ | ✓ |
| Display name of reporter | ✓ | own | ✓ |
| Chat thread | — | — | — (Phase 06) |

Claimant cannot claim own report. Claim refused on non-`Published` status without consuming attempt.

---

## 6. Notifications (Section 5.7)

| Event | Recipient | Introduced |
| ----- | --------- | ---------- |
| New claim submitted | Reporter | this phase |
| Claim withdrawn by claimant | Reporter | this phase |
| Claim approved | Claimant | this phase |
| Claim rejected | Claimant | this phase |
| Claim closed — report unavailable | Claimant | this phase (`ClaimCleanupService` unit-tested; E2E on `Published` withdraw in Phase 07) |
| Claim cancelled by counterparty | Other party | deferred to Phase 06 (full cancel flow) |
| Claim auto-withdrawn | Reporter and claimant | deferred to Phase 07 (job) |

---

## 7. Out of scope

Explicitly deferred to later phases:

- Real-time chat messaging → Phase 06
- Mutual resolution / Confirm Resolved → Phase 06
- Cancel approved claim (full flow with chat read-only) → Phase 06
- 10-day pending-claim auto-withdraw job → Phase 07
- Pending-claim closure on expiry/takedown/ban → Phase 07/08
- Abuse report-from-chat → Phase 08
- Claim-ended-by-enforcement notification → Phase 08

---

## 8. Acceptance criteria

From [SPEC.md Section 15.4](../../SPEC.md#154-claiming-and-review).

- [ ] **Claim creation constraints:** a logged-in non-reporter submitting 10–500 characters on a `Published` report creates a `Pending` claim. Claim creation is refused on any other status without consuming an attempt, and refused when that user already has an open `Pending` claim on the report
- [ ] **Direction-specific prompt:** the claim form asks the finder to describe the item they found on a lost report, and asks the owner to describe and prove the item on a found report
- [ ] **Claim photo:** at most one photo may be attached, and it is visible only to the reporter and, during a flagged-listing investigation, the admin
- [ ] **Claimant withdrawal:** withdrawing a pending claim sets it to `Withdrawn`, consumes no attempt, and notifies the reporter
- [ ] **Approval side effects:** the claim becomes `Approved`, the report becomes `Claim In Progress`, a chat thread is created, and all other pending claims become `Rejected` with the reason `Another claim approved`, notified, with no attempt consumed
- [ ] **Attempt counting:** manual reporter rejections and claimant-initiated cancellations of an approved claim each consume one attempt; reporter-initiated cancellations, claimant withdrawals, **10-day auto-withdrawals**, auto-rejections, auto-closures, and refused claims do not. After 3 counted failures, further claims on that report by that user are blocked with a clear message
- [ ] **Daily claim quota:** at 5 claim submissions in the current Africa/Cairo day, the next claim is rejected with clear quota messaging
- [ ] **Pending claim closure:** `ClaimCleanupService` closes pending claims with no attempt consumed and sends `ClaimClosedReportUnavailable` (unit/integration test against the service in sub-phase 09). **E2E** closure when a `Published` report is withdrawn, expired, or taken down → Phase 07/08 (withdraw of `Published` is not available until Phase 07)

**Deferred within v1:**

- [ ] **Reporter timeout (10-day auto-withdraw)** → Phase 07. Test via configurable interval or admin trigger stub documented in sub-phase 09.

### 10-day timeout test stub

For CI and manual QA before Phase 07 ships the job:

- Environment variable `CLAIM_TIMEOUT_MINUTES` (default: 14400 = 10 days) overrides the timeout interval in non-production
- Optional admin-only `POST /api/admin/test/trigger-claim-timeout` for early CI hooks (Phase 07's `POST /api/admin/test/run-job/{jobName}` is the primary test trigger)

---

## 9. Definition of done

### Automated tests

- [ ] Claim creation on `Published` only; refused on other statuses
- [ ] One open `Pending` claim per user per report
- [ ] Direction-specific validation and contact-info block
- [ ] Approve: report → `Claim In Progress`, thread created, others auto-rejected
- [ ] Reject: attempt consumed, notification sent
- [ ] Withdraw: no attempt consumed
- [ ] 3-attempt limit enforced
- [ ] Daily quota (5/day)
- [ ] Claim photo private; pre-signed URL access control
- [ ] `ClaimCleanupService.ClosePendingClaimsAsync` (direct service test; not E2E withdraw)

### Manual smoke checklist

- [ ] Submit claim on published lost report as finder
- [ ] Submit claim on published found report as owner
- [ ] Reporter sees claims in report detail; approves one
- [ ] Other pending claims auto-rejected with notification
- [ ] Report shows "claim in progress" in browse
- [ ] Chat thread record exists in DB but chat UI shows "coming soon" or is hidden

### Phase exit gate

This phase is complete when all acceptance criteria pass and no out-of-scope items were implemented early.
