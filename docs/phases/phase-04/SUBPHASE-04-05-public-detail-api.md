# Sub-phase 05 — Public Detail API & URL Routing

**Status:** Not started  
**Prerequisites:** [Sub-phase 04 — Public DTO](./SUBPHASE-04-04-public-dto.md), Phase 02/03 reporter detail endpoints

---

## 1. Summary

Implement status-aware public detail endpoints: `GET /api/reports/{id}/public`, `GET /api/lost/{id}`, and `GET /api/found/{id}`. Returns public DTO for visible reports; distinct error codes for not-found vs permanently-unavailable. **Optional authentication** enables reporter/admin carve-out for `Pending Review`/`Rejected` (SPEC 4.4 / 15.3).

---

## 2. SPEC references

| SPEC section | Topic |
| ------------ | ----- |
| Section 4.4 | Public URLs `/lost/{id}`, `/found/{id}`; status routing |
| Section 9 | Visibility by role and status |
| Section 15.3 | URL behavior acceptance criteria |

**Contract reference:** [api-error-contract.md](../../api-error-contract.md) — add `report.not_found`, `report.unavailable`

---

## 3. What you will learn

- Status → HTTP response mapping for public visitors
- Type validation: `/lost/{id}` must be lost report
- Optional Bearer token on public routes for reporter/admin pending/rejected access
- Use `404` + `report.unavailable` for terminal statuses (Angular routes to `/unavailable` on code, not HTTP status alone)

**Files to read after implementing:**

- `api/Controllers/PublicReportsController.cs`
- `api/Services/Browse/PublicReportAccessService.cs`
- `api.Tests/Controllers/PublicReportAccessTests.cs`

---

## 4. Deliverables

### Endpoints

| Method | Route | Auth | Purpose |
| ------ | ----- | ---- | ------- |
| GET | `/api/reports/{id}/public` | Optional | Type-agnostic public detail |
| GET | `/api/lost/{id}` | Optional | Lost report; 404 if type mismatch |
| GET | `/api/found/{id}` | Optional | Found report; 404 if type mismatch |

All three share `PublicReportAccessService.GetPublicDetailAsync(id, expectedType?, user?)`.

Pass `Authorization` header when present so reporter/admin carve-out applies.

### Status routing

| Status | Anonymous / stranger | Reporter (own) | Admin |
| ------ | -------------------- | -------------- | ----- |
| `Published` | `200` public DTO | `200` public DTO | `200` public DTO |
| `Claim In Progress` | `200` + `claimInProgress: true` | same | same |
| `Pending Review`, `Rejected` | `404` `report.not_found` | `200` reporter detail DTO (Phase 02 shape) | `200` admin/reporter-equivalent view |
| `Resolved`, `Withdrawn`, `Removed by Admin` | `404` `report.unavailable` | same | same |
| Missing ID | `404` `report.not_found` | same | same |
| Wrong type | `404` `report.not_found` | same | same |

### Response body for unavailable (flat envelope)

```json
{
  "code": "report.unavailable",
  "message": "..."
}
```

HTTP status: **`404`** (caller lacks visibility / resource unavailable — per error contract). Angular sub-phase 08 routes to `/unavailable` on `code === "report.unavailable"`.

### `PublicReportDetailDto`

From sub-phase 04 mapper — includes `claimInProgress`, all public fields, no private data.

---

## 5. Step-by-step implementation order

1. Implement `PublicReportAccessService` with status switch + optional user context
2. Add type check for `/lost` and `/found` routes
3. Add `PublicReportsController` with three actions (optional auth middleware)
4. Add error codes to api-error-contract
5. Integration tests for every status × role combination
6. Test wrong-type URL returns not-found
7. Test private photos absent in public response JSON
8. Test reporter own `Pending Review` via `/api/lost/{id}` with token → `200`

---

## 6. Out of scope

- Claim submission (Phase 05)
- Angular detail pages (sub-phase 08)
- SEO / Open Graph meta (out of scope v1)

---

## 7. Validation gate

### Automated tests

- [ ] Published lost report via `/api/lost/{id}` → `200`
- [ ] Same report via `/api/found/{id}` → `404` wrong type
- [ ] `Claim In Progress` → `200`, `claimInProgress: true`
- [ ] `Pending Review` stranger → `404 report.not_found`
- [ ] `Pending Review` reporter own + auth → `200`
- [ ] `Pending Review` admin + auth → `200`
- [ ] `Resolved` → `404 report.unavailable`
- [ ] `Withdrawn` → `404 report.unavailable`
- [ ] Random UUID → `404`
- [ ] No `hiddenVerificationDetail` in response body
- [ ] Documents/IDs published report → no photos in public response

### Manual smoke checklist

- [ ] Approve report in Phase 03; fetch via `/api/lost/{id}` — full public detail
- [ ] Withdraw report; fetch again — unavailable code

---

## 8. Exit criteria

- [ ] All URL routing tests pass (SPEC 15.3)
- [ ] Mark sub-phase 05 complete in [phase-04/README.md](./README.md)
