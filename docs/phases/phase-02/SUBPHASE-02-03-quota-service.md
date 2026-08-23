# Sub-phase 03 — Submission Quota Service

**Status:** Not started  
**Prerequisites:** Phase 01 complete (schema + `CairoTime` utilities)

---

## 1. Summary

Implement `ISubmissionQuotaService` enforcing the daily new-report quota (3 per Cairo day) and concurrent open-report cap (5). Pure service layer with integration tests against the database — no HTTP endpoint yet. Wired into `POST /api/reports` in sub-phase 05.

---

## 2. SPEC references

| SPEC section | Topic |
| ------------ | ----- |
| Section 4.1.6 | Daily submission quota (3 new reports / Cairo day) |
| Section 4.1.7 | Open-report cap (5 in Pending Review, Published, Claim In Progress) |
| Section 15.1 | Quota acceptance criteria |

**Contract reference:** [api-error-contract.md](../../api-error-contract.md) — `report.quota_daily_exceeded`, `report.quota_open_cap_exceeded`

---

## 3. What you will learn

- Counting reports by Cairo calendar day using `CairoTime.GetCairoDayStartUtc` / `GetCairoDayEndUtc`
- Which statuses count toward the open cap vs daily quota
- Why resubmissions (Phase 03) bypass daily quota but not implemented yet — design the interface now
- Returning quota metadata for clear error messages (`Retry-After` until next Cairo midnight for daily limit)

**Files to read after implementing:**

- `api/Services/Reports/ISubmissionQuotaService.cs`
- `api/Services/Reports/SubmissionQuotaService.cs`
- `api.Tests/Services/SubmissionQuotaServiceTests.cs`

---

## 4. Deliverables

### `ISubmissionQuotaService`

```csharp
public interface ISubmissionQuotaService
{
    Task<QuotaCheckResult> CheckNewSubmissionAsync(Guid userId, CancellationToken ct = default);
}

public record QuotaCheckResult(bool Allowed, string? ErrorCode, int? RetryAfterSeconds);
```

### Daily quota (3 new reports / Cairo day)

Count `Report` rows where:

- `ReporterUserId == userId`
- `CreatedAt` within current Cairo calendar day (UTC bounds from `CairoTime`)
- **New submissions only** — for Phase 02, every create counts (resubmit exclusion added in Phase 03)

At count ≥ 3:

- `Allowed = false`
- `ErrorCode = "report.quota_daily_exceeded"`
- `RetryAfterSeconds` = seconds until next Cairo midnight

### Open-report cap (5 concurrent)

Count `Report` rows where:

- `ReporterUserId == userId`
- `Status` in (`Pending Review`, `Published`, `Claim In Progress`)

At count ≥ 5:

- `Allowed = false`
- `ErrorCode = "report.quota_open_cap_exceeded"`
- `RetryAfterSeconds = null` (user must withdraw/resolve existing reports)

### Check order

1. Open cap first (more actionable message)
2. Then daily quota

### Statuses that do **not** count toward open cap

- `Rejected`, `Withdrawn`, `Resolved`, `Removed by Admin`

---

## 5. Step-by-step implementation order

1. Define `ISubmissionQuotaService` and `QuotaCheckResult`
2. Implement EF queries with indexed `ReporterUserId` + `Status` + `CreatedAt`
3. Use `CairoTime` for day boundaries (never `DateTime.Now` without timezone)
4. Register in DI as scoped service
5. Write integration tests with `WebApplicationFactory` or in-memory DB
6. Seed test users with 0, 2, 3, 5 reports in various statuses
7. Do not add controller action yet

---

## 6. Out of scope

- Resubmit bypass (Phase 03) — add `CheckResubmissionAsync` stub or comment for later
- Claim daily quota (Phase 05)
- HTTP 429 mapping (sub-phase 05 controller)
- Angular quota messaging (sub-phases 08–09)

---

## 7. Validation gate

### Automated tests

- [ ] User with 0 reports → allowed
- [ ] User with 3 reports created today (Cairo) → daily quota blocked
- [ ] Report created yesterday does not count toward today's quota
- [ ] User with 5 `Pending Review` reports → open cap blocked
- [ ] `Rejected` reports do not count toward open cap
- [ ] `Published` + `Claim In Progress` count toward open cap
- [ ] `RetryAfterSeconds` for daily quota is positive and < 86400
- [ ] Open cap returns `report.quota_open_cap_exceeded` without `Retry-After`

### Manual smoke checklist

- [ ] Read `SubmissionQuotaService.cs` query logic — confirm status enum matches schema

---

## 8. Exit criteria

- [ ] All integration tests pass
- [ ] Service registered in DI
- [ ] Mark sub-phase 03 complete in [phase-02/README.md](./README.md)
