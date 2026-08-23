# Sub-phase 08 — Claim Submission UI

**Status:** Not started  
**Prerequisites:** [Sub-phase 04](./SUBPHASE-05-04-claim-create.md), Phase 04 [detail UI](../phase-04/SUBPHASE-04-08-detail-ui.md)

---

## 1. Summary

Activate the claim CTA on `/lost/{id}` and `/found/{id}`: direction-specific form, 10–500 char answer, optional photo, submit to API. Replace Phase 04 disabled stub.

---

## 2. SPEC references

| SPEC section | Topic |
| ------------ | ----- |
| Section 6.2 | Direction-specific prompts |
| Section 4.5 | Claim on published reports |
| Section 15.4 | Direction-specific + photo |

---

## 3. What you will learn

- Lost report: prompt finder to describe what they found
- Found report: prompt owner to describe and prove ownership
- Reusing photo upload from Phase 02 pattern (claim variant)
- Hiding CTA when `claimInProgress` or user is reporter

**Files to read after implementing:**

- `web/src/app/features/report-detail/claim-form/`
- `web/src/app/features/report-detail/public-report.service.ts` (extend)

---

## 4. Deliverables

### Claim form (modal or inline section)

| Report type | Prompt (Arabic) |
| ----------- | --------------- |
| Lost | صف العنصر الذي وجدته… |
| Found | صف العنصر وبرّه ملكيتك… |

- Textarea 10–500 with counter
- Optional single photo upload
- Submit → `POST /api/reports/{id}/claims`
- Success message + redirect to `/my/claims`

### CTA visibility

| State | CTA |
| ----- | --- |
| Logged out | Login prompt (Phase 04 behavior) |
| `Published` | Show form |
| `Claim In Progress` | Hidden/disabled with label |
| Reporter viewing own | Hidden |
| Attempt limit reached | Disabled + message |

---

## 5. Step-by-step implementation order

1. Build `ClaimFormComponent`
2. Wire claim photo upload service
3. Integrate into lost/found detail pages
4. Handle API errors (quota, attempt limit, duplicate)
5. Manual test both directions

---

## 6. Out of scope

- Claim review (sub-phase 07)
- Chat (Phase 06)

---

## 7. Validation gate

- [ ] Submit claim on published lost report
- [ ] Submit claim on published found report with photo
- [ ] CIP report — no submit button
- [ ] Quota error shown clearly

---

## 8. Exit criteria

- [ ] Claim submission E2E works
- [ ] Mark sub-phase 08 complete in [phase-05/README.md](./README.md)
