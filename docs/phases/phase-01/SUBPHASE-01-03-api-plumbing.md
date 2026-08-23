# Sub-phase 03 — API Plumbing

**Status:** Not started  
**Prerequisites:** [Sub-phase 02 — Monorepo Scaffold](./SUBPHASE-01-02-scaffold.md)

---

## 1. Summary

Add cross-cutting API infrastructure used by every later endpoint: global exception mapping to the error contract, generic rate-limit middleware returning HTTP 429 with `Retry-After`, request logging, and an integration test project.

---

## 2. SPEC references

| SPEC section | Topic |
| ------------ | ----- |
| Section 20.2 | HTTP 429 + `Retry-After` rate-limit response pattern |
| Section 14 | API error contract (resolved in sub-phase 01) |

**Contract reference:** [docs/api-error-contract.md](../../api-error-contract.md)

---

## 3. What you will learn

- ASP.NET Core middleware pipeline order (exception handler before routing)
- Custom exception types mapped to stable error codes
- `ProblemDetails` vs a fully custom error envelope — and why Amanah uses custom
- Integration testing with `WebApplicationFactory<T>`
- How `Retry-After` headers communicate backoff to clients

**Files to read after implementing:**

- `api/Middleware/ExceptionHandlingMiddleware.cs`
- `api/Middleware/RateLimitMiddleware.cs`
- `api/Models/ApiError.cs`
- `api.Tests/` — integration test setup

---

## 4. Deliverables

### Error contract implementation

| Component | Purpose |
| --------- | ------- |
| `ApiError` record | `{ Code, Message, Errors? }` matching [api-error-contract.md](../../api-error-contract.md) |
| `ApiException` base | Carries `Code`, `Message`, `StatusCode`, optional field `Errors` |
| `ExceptionHandlingMiddleware` | Catches all exceptions; maps known types to contract; unknown → `500 internal.error` |
| `ValidationBehavior` or filter | Maps `ModelState` / FluentValidation failures → `400 validation.failed` |

### Rate-limit middleware

| Item | Detail |
| ---- | ------ |
| Scope | Generic IP-based sliding window (e.g. 100 requests / minute per IP) |
| Response | `429` + `rate_limit.exceeded` + `Retry-After` header (seconds) |
| Purpose | Infrastructure pattern from SPEC 20.2; OTP-specific limits are separate (sub-phase 06) |
| Config | Window size and max requests in `appsettings.json` |

### Request logging

- Log method, path, status code, and duration per request
- Do not log request bodies (may contain phone numbers later)

### Integration test project

| Item | Detail |
| ---- | ------ |
| Project | `api.Tests` (xUnit + `Microsoft.AspNetCore.Mvc.Testing`) |
| Fixture | `WebApplicationFactory` with test configuration |
| Tests | See validation gate below |

### Test-only endpoint (remove or protect in production)

- `POST /api/_test/validate` — accepts a DTO with `[Required]` fields; used to test validation error shape
- `GET /api/_test/rate-limit` — always returns 429 when called (or use middleware with artificially low limit in test config)

---

## 5. Step-by-step implementation order

1. Create `ApiError` record and `ApiException` hierarchy
2. Implement `ExceptionHandlingMiddleware`; register first in pipeline
3. Add validation filter/behavior for model binding errors
4. Implement `RateLimitMiddleware` with in-memory store (Redis not needed for v1)
5. Add request logging middleware
6. Create `api.Tests` project with `WebApplicationFactory`
7. Write integration tests for error shapes and rate limit
8. Add test-only endpoints behind `#if DEBUG` or `ASPNETCORE_ENVIRONMENT=Testing`

---

## 6. Out of scope

- Auth-specific errors (`auth.*`, `otp.*`) — sub-phases 06–08
- OTP send limits — sub-phase 06
- EF Core / database
- Angular changes

---

## 7. Validation gate

### Automated tests

- [ ] Validation failure returns `400` with `{ code: "validation.failed", message, errors: { field: [...] } }`
- [ ] Unhandled exception returns `500` with `{ code: "internal.error", message }` — no stack trace in body
- [ ] Rate limit exceeded returns `429` with `Retry-After` header and `{ code: "rate_limit.exceeded" }`
- [ ] `Retry-After` value is a positive integer (seconds)

### Manual smoke checklist

- [ ] `curl -i` on rate-limited endpoint shows `Retry-After` header
- [ ] Error JSON matches shape in [api-error-contract.md](../../api-error-contract.md)

---

## 8. Exit criteria

- [ ] All automated tests pass
- [ ] Manual smoke checklist complete
- [ ] Mark sub-phase 03 complete in [phase-01/README.md](./README.md)
