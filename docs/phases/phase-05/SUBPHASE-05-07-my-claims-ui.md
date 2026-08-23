# Sub-phase 07 — My Claims & Reporter Review UI

**Status:** Not started  
**Prerequisites:** API sub-phases [05](./SUBPHASE-05-05-approve-reject.md)–[06](./SUBPHASE-05-06-withdraw-read.md)

---

## 1. Summary

Build `/my/claims`, reporter Claims section on `/my/reports/{id}`, and Claim In Progress tab on My Reports. Chat link shows "قريباً" or is hidden until Phase 06.

---

## 2. SPEC references

| SPEC section | Topic |
| ------------ | ----- |
| Section 4.8 | My Claims; Claims section in report detail |
| Section 6.5 | Claim In Progress presentation |

---

## 3. What you will learn

- Claims list with status badges
- Reporter approve/reject actions from report detail
- Claim photo preview via presigned URL (reporter view)
- Enabling Claim In Progress tab (stubbed in Phase 02/03)

**Files to read after implementing:**

- `web/src/app/features/my-claims/`
- `web/src/app/features/my-reports/claims-section/`

---

## 4. Deliverables

### `/my/claims`

- List all user's claims with status, report title, date
- Link to report public URL or claim detail

### `/my/reports/{id}` — Claims section

- Visible when report has claims or is `Published`/`Claim In Progress`
- List pending claims with answer + photo
- Approve / Reject buttons per pending claim
- Confirmation dialogs

### `/my/reports` — Claim In Progress tab

- Enable tab (was stub); list reports in `Claim In Progress`
- Note: "المحادثة متاحة قريباً" until Phase 06

---

## 5. Step-by-step implementation order

1. `ClaimsService` for API calls
2. My Claims page
3. Claims section component on report detail
4. Enable CIP tab on My Reports
5. Manual E2E with two test users

---

## 6. Out of scope

- Claim submission form (sub-phase 08)
- Chat UI (Phase 06)

---

## 7. Validation gate

- [ ] Reporter approves claim from UI
- [ ] Auto-rejected claimants see notification (Phase 03 center)
- [ ] CIP tab shows approved report
- [ ] No chat UI wired

---

## 8. Exit criteria

- [ ] Reporter review UI complete
- [ ] Mark sub-phase 07 complete in [phase-05/README.md](./README.md)
