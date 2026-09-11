# Phase 04 - Claims & Verification

**Status:** Complete — code and automated tests shipped; manual smoke checklist ready for QA (Phase 05/06 deferrals documented below)  
**Prerequisites:** Phase 03 - Browse & Discovery

---

## 1. Summary

Logged-in users submit claims on `Published` reports; reporters manually approve or reject; attempt limits and daily quota apply; approving moves the report to `Claim In Progress` and creates an inert `ChatThread` placeholder for Phase 05. Claim photos upload in the same multipart submit as the claim text. Frontend covers claim submission, My Claims, and reporter review on report detail.

---

## 2. SPEC references

| SPEC section | Topic |
| ------------ | ----- |
| Section 4.5 | Claiming / verification (reference to Section 6) |
| Section 4.8 | My Claims; report-detail Claims section |
| Section 6 | Verification and claim model (entire section) |
| Section 7.5 | Daily claim quota |
| Section 8 | `Published` <-> `Claim In Progress` transitions |
| Section 9 | Claimant/reporter claim visibility |
| Section 15.4 | Claiming acceptance criteria (except 10-day timeout job) |
| Section 19 | Claim photo upload (private, pre-signed) |

**Part II (technical):** Section 19 (claim photo)

---

## 3. Prerequisites

### Prior phases

- [x] Platform foundation
- [x] Phase 01 - Report Submission
- [x] Phase 02 - Admin Moderation
- [x] Phase 03 - Browse & Discovery

### Deferred decisions (Section 14)

None additional.

---

## 4. Deliverables

### API

| Method | Route | Purpose | Status |
| ------ | ----- | ------- | ------ |
| POST | `/api/v1/reports/{id}/claims` | Submit claim (`multipart/form-data`: JSON `claim` + optional `photo`; **200** `{ id, status }`) | Shipped |
| GET | `/api/v1/reports/{id}/claims` | Reporter: list claims on own report | Shipped |
| POST | `/api/v1/claims/{id}/approve` | Reporter approves → `Claim In Progress` | Shipped |
| POST | `/api/v1/claims/{id}/reject` | Reporter rejects claim | Shipped |
| POST | `/api/v1/claims/{id}/withdraw` | Claimant withdraws `Pending` claim | Shipped |
| GET | `/api/v1/claims/mine` | Claimant's claims (My Claims) | Shipped |
| GET | `/api/v1/claims/{id}` | Claim detail (claimant or reporter) | Shipped |
| GET | `/api/v1/uploads/claim-photo/{id}/url` | Pre-signed URL (claimant, reporter; admin 403 stub) | Shipped |

**Claim submit multipart** (mirrors report create):

- Part `claim`: JSON `{ "submittedAnswer": "..." }`
- Part `photo`: optional single image (max **one**; same format/size rules as report photos)
- Rate limiting: `photo-upload` policy when a photo part is present
- Invalid photo → **400** on `photo`; **no claim row created**

### UI routes

| Route | Access | Purpose | Status |
| ----- | ------ | ------- | ------ |
| `/lost/{id}` / `/found/{id}` | Logged-in | Claim submission form on `Published` reports | Shipped |
| `/my/claims` | Logged-in | My Claims list + withdraw pending | Shipped |
| `/my/reports/{id}` | Logged-in (reporter) | Claims section with approve/reject + presigned photos | Shipped |
| `/my/reports` | Logged-in | `claim_in_progress` tab | Shipped |

### Database

- `Claim` records with status, attempt tracking, `CountsAsFailure` flag
- `ChatThread` record created on claim approval (messaging inert until Phase 05)
- `Report.status` → `Claim In Progress` on approval
- Auto-reject other `Pending` claims with reason `Another claim approved`

### Infrastructure

- R2 `private/claims/{claimId}/{uploadId}` for claim photos (original + `_thumb.webp`)
- Pre-signed URLs (5-minute expiry) via `GET /uploads/claim-photo/{claimId}/url`
- **Known gap:** photo written to R2 before `SaveChangesAsync`; compensating cleanup → Phase 06

### Shared utilities

- Direction-specific claim prompt copy (lost vs found) — **frontend i18n**; backend length/contact validation only
- Contact-info block on claim text
- Daily claim quota: 5 submissions/day (Africa/Cairo)
- One open `Pending` claim per user per report
- 3 attempts per user per report lifetime (`CountsAsFailure` rows)
- `ClaimCleanupService.ClosePendingClaimsAsync` — unit-tested; wired to report lifecycle in Phase 06

### Phase 03 / Phase 04 boundary

> On claim approval, a `ChatThread` row is created but no SignalR hub, message endpoints, or chat UI are wired. Phase 05 activates messaging.

---

## 5. Permissions (Section 9)

| Data | Claimant | Reporter | Admin |
| ---- | -------- | -------- | ----- |
| Claim text and claim photo | own | yes (for review) | yes (flagged-listing investigation only — stub 403 until Phase 07) |
| Display name of claimant | own | yes | yes |
| Display name of reporter | yes | own | yes |
| Chat thread | — | — | — (Phase 05) |

---

## 6. Notifications (Section 5.7)

| Event | Recipient | Status |
| ----- | --------- | ------ |
| New claim submitted | Reporter | Shipped (`NewClaimSubmitted` → `/my/reports/{id}#claims-section`) |
| Claim withdrawn by claimant | Reporter | Shipped |
| Claim approved | Claimant | Shipped (deep link `/my/chats/{id}` — UI redirects to `/my/claims` until Phase 05) |
| Claim rejected | Claimant | Shipped |
| Claim closed - report unavailable | Claimant | Shipped (`ClaimCleanupService` unit-tested; E2E on `Published` withdraw → Phase 06) |
| Claim cancelled by counterparty | Other party | Phase 05 |
| Claim auto-withdrawn | Reporter and claimant | Phase 06 |

---

## 7. Out of scope

- Real-time chat messaging → Phase 05
- Mutual resolution / Confirm Resolved → Phase 05
- Cancel approved claim → Phase 05
- 10-day pending-claim auto-withdraw job → Phase 06
- Pending-claim closure on expiry/takedown/ban (E2E) → Phase 05/07
- Abuse report-from-chat → Phase 07
- Backend direction-specific answer validation (lost vs found wording) — deferred; frontend prompts shipped

---

## 8. Acceptance criteria

- [x] **Claim creation constraints:** 10–500 chars on `Published` report; refused on other statuses without attempt; one open `Pending` per user per report
- [x] **Direction-specific prompt:** frontend asks finder vs owner wording (backend does not enforce report-type-specific content)
- [x] **Claim photo:** max one; private; reporter sees via presigned URL in review UI
- [x] **Claimant withdrawal:** `Withdrawn`, no attempt consumed, reporter notified
- [x] **Approval side effects:** `Approved`, report `Claim In Progress`, `ChatThread` created, other pending → `Rejected` with `Another claim approved`, no attempt on auto-reject
- [x] **Attempt counting:** manual reject and claimant cancel of approved claim count; withdraw, auto-reject, cleanup, refused claims do not
- [x] **Daily claim quota:** 5/day Cairo
- [x] **Pending claim closure:** `ClaimCleanupService` unit-tested; E2E on live report withdraw → Phase 06

**Deferred:** 10-day reporter timeout → Phase 06

---

## 9. Definition of done

### Automated tests

- [x] Claim creation on `Published` only; refused on other statuses
- [x] One open `Pending` claim per user per report
- [x] Contact-info block on claim text
- [x] Approve: report → `Claim In Progress`, thread created, others auto-rejected
- [x] Reject: attempt consumed, notification sent
- [x] Withdraw: no attempt consumed
- [x] 3-attempt limit enforced
- [x] Daily quota (5/day)
- [x] Claim photo multipart submit; invalid/>1 photo rejected without claim row
- [x] Claim photo presign ACL (claimant, reporter; admin 403)
- [x] `ClaimCleanupService.ClosePendingClaimsAsync`
- [x] Reporter list claims on own report (`GET /reports/{id}/claims`)
- [x] Submit notifies reporter (`NewClaimSubmitted`)

### Implementation map

| Area | Location |
| ---- | -------- |
| Submit | `ReportsController.SubmitClaim`, `ClaimSubmitFormParser` |
| List for reporter | `ReportsController.GetReportClaims`, `ClaimService.GetByReportAsync` |
| Review | `ClaimsController` approve/reject, `ClaimService` |
| Withdraw / reads | `ClaimsController` withdraw/mine/detail |
| Cleanup | `ClaimCleanupService` |
| Photo | `ClaimPhotoAttachService`, `ClaimPhotoPresignService` |
| Contracts | `SubmitClaimRequest`, `ReportClaimSummaryResponse`, `ClaimDetailResponse`, `MyClaimSummaryResponse` |
| Frontend | `web/src/app/claims/`, `report-claims-section`, `public-report-detail` claim form |

### Manual smoke checklist

- [ ] Submit claim on published lost report as finder
- [ ] Submit claim on published found report as owner
- [ ] Reporter sees claims in report detail; approves one
- [ ] Other pending claims auto-rejected with notification
- [ ] Report shows "claim in progress" in browse
- [ ] Chat thread exists in DB; no chat UI (claimant sees `/my/claims` from approval notification)

### Phase exit gate

Phase 04 code and automated tests are complete. Remaining manual smoke is QA before Phase 05.
