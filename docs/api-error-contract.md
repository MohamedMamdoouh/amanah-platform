# API Error Contract

**Status:** v1 · **Phase:** 01 · **Standard:** [RFC 9457](https://www.rfc-editor.org/rfc/rfc9457)

Reference for consistent error handling across the API and Angular client.

---

## Principles

| Rule              | Detail                                                        |
| ----------------- | ------------------------------------------------------------- |
| Format            | RFC 9457 Problem Details (`application/problem+json`)         |
| Language          | English only in API responses — frontend localizes via `code` |
| Business failures | `Result` / `Result<T>` in services — do not throw             |
| Unexpected faults | `ExceptionHandlingMiddleware` → `internal.error`              |
| No magic strings  | Use `ErrorCodes`, `ErrorCatalog`, `FieldNames` constants      |
| Secrets           | Never expose stack traces, phone numbers, or OTP codes        |

Success responses are **not** wrapped in Problem Details (direct JSON or `204`).

---

## Constants

Wire codes and field keys are defined once as constants — never as raw strings in services, tests, middleware, or Angular.

| Constant          | Where used                                  | Example                                         |
| ----------------- | ------------------------------------------- | ----------------------------------------------- |
| `ErrorCodes`      | Tests, comparisons, building `ErrorCatalog` | `ErrorCodes.Auth.InvalidPhone`                  |
| `ErrorCatalog`    | Service failure returns                     | `return ErrorCatalog.Auth.InvalidPhone`         |
| `FieldNames`      | Validation `errors` map keys                | `[FieldNames.DisplayName]`                      |
| `ErrorCodes` (TS) | Angular branching                           | `problem.code === ErrorCodes.Auth.TokenExpired` |

Never write `"auth.invalid_phone"` or `"displayName"` as literals in application code.

---

## Problem Details shape

```json
{
  "type": "https://amanah.app/api/errors/validation.failed",
  "title": "Validation failed",
  "status": 400,
  "detail": "Please correct the errors in the form.",
  "instance": "/api/auth/register",
  "code": "validation.failed",
  "errors": {
    "displayName": ["Display name is required."]
  }
}
```

| Field      | Required | Description                                                                    |
| ---------- | -------- | ------------------------------------------------------------------------------ |
| `type`     | Yes      | `https://amanah.app/api/errors/{code}` (config: `ErrorCatalog:BaseUri`)        |
| `title`    | Yes      | Short English summary                                                          |
| `status`   | Yes      | HTTP status (must match response)                                              |
| `detail`   | Yes      | English explanation for this occurrence                                        |
| `instance` | No       | Request path                                                                   |
| `code`     | Yes      | Stable identifier for branching and i18n                                       |
| `errors`   | No       | Field validation map — camelCase keys, English message arrays. Omit when empty |

---

## Error catalog

| Item  | Value                                        |
| ----- | -------------------------------------------- |
| Route | `GET /api/errors`                            |
| Auth  | Public (`AllowAnonymous`)                    |
| Cache | `Cache-Control: public, max-age=3600` + ETag |
| Scope | Client-visible codes only                    |

Returns `{ "errors": [{ "code", "type", "title", "status", "defaultDetail" }] }`. No per-code route — clients match by `code`.

`ErrorCatalog` / `IErrorCatalog` is the single source of truth for catalog entries and Problem Details mapping.

---

## Source files

### API (`api/Errors/`)

| File                         | Purpose                                                                    |
| ---------------------------- | -------------------------------------------------------------------------- |
| `ErrorCodes.cs`              | `const string` wire codes only                                             |
| `FieldNames.cs`              | `const string` validation field keys                                       |
| `ErrorCatalog.cs`            | `static readonly ErrorDefinition` (code + title + status + default detail) |
| `IErrorCatalog.cs`           | Registry for `GET /api/errors` and Problem Details                         |
| `ErrorDefinition.cs`         | Record + `ToError()` for occurrence-specific detail / field errors / retry |
| `Error.cs`                   | Failure carrier                                                            |
| `Result.cs` / `Result<T>.cs` | Result types with implicit conversions                                     |
| `ResultExtensions.cs`        | `ToActionResult()` → Problem Details                                       |

### Angular (`web/src/app/core/errors/`)

| File             | Purpose                              |
| ---------------- | ------------------------------------ |
| `error-codes.ts` | `as const` mirror of `ErrorCodes.cs` |
| `field-names.ts` | Mirror of `FieldNames.cs`            |

---

## Usage reference

```csharp
// Service — implicit conversions
if (!PhoneNormalizer.IsValid(request.Phone))
    return ErrorCatalog.Auth.InvalidPhone;
return response;

// Occurrence-specific data — use ToError()
return ErrorCatalog.Validation.Failed.ToError(
    fieldErrors: new Dictionary<string, string[]>
    {
        [FieldNames.DisplayName] = ["Display name is required."]
    });

return ErrorCatalog.Otp.Cooldown.ToError(
    detail: $"Please wait {seconds} seconds before requesting a new code.",
    retryAfterSeconds: seconds);

// Controller
return (await _service.RegisterAsync(request)).ToActionResult();
```

```typescript
// Angular — branch on constants, localize via i18n (not raw detail)
if (problem.code === ErrorCodes.Auth.TokenExpired) this.auth.refresh();
if (problem.code === ErrorCodes.Report.Unavailable)
  this.router.navigate(["/unavailable"]);
```

**Adding a new error:** `ErrorCodes` → `ErrorCatalog` → row in registry below → Angular mirror → i18n key (`errors.{code}`).

---

## HTTP status mapping

| Status | When                                      | Code prefix examples                      |
| ------ | ----------------------------------------- | ----------------------------------------- |
| 400    | Validation, client-fixable business rules | `validation.*`, `auth.*`                  |
| 401    | Missing / invalid / expired token         | `auth.unauthorized`, `auth.token_expired` |
| 403    | Wrong role or banned                      | `auth.forbidden`, `auth.banned`           |
| 404    | Not found or caller lacks visibility      | `report.*`, `not_found`                   |
| 409    | State conflict                            | `conflict.*`                              |
| 429    | Rate limit or quota                       | `rate_limit.*`, `otp.*`                   |
| 503    | External dependency down                  | `service.*`                               |
| 500    | Unexpected server fault                   | `internal.error`                          |

**429 responses** must include `Retry-After` (seconds). Set from `Error.RetryAfterSeconds` (SPEC Section 20.2).

---

## Error code registry

### Phase 01 — Auth & OTP

| Code                       | HTTP | Description                                 |
| -------------------------- | ---- | ------------------------------------------- |
| `validation.failed`        | 400  | One or more fields failed validation        |
| `auth.invalid_phone`       | 400  | Phone number format not accepted            |
| `auth.captcha_failed`      | 400  | CAPTCHA verification failed                 |
| `auth.invalid_otp`         | 400  | OTP code incorrect                          |
| `auth.otp_expired`         | 400  | OTP code expired                            |
| `auth.otp_void`            | 400  | OTP voided after 3 failed attempts          |
| `auth.provisional_invalid` | 400  | Provisional signup token invalid or expired |
| `auth.unauthorized`        | 401  | No valid access token                       |
| `auth.token_expired`       | 401  | Access token expired                        |
| `auth.refresh_invalid`     | 401  | Refresh token invalid, expired, or revoked  |
| `auth.banned`              | 403  | Account is banned                           |
| `auth.forbidden`           | 403  | Insufficient role or permission             |
| `otp.cooldown`             | 429  | Resend within 120-second cooldown           |
| `otp.hourly_limit`         | 429  | More than 2 sends in rolling hour           |
| `otp.daily_limit`          | 429  | More than 3 sends in rolling Cairo day      |
| `rate_limit.exceeded`      | 429  | Generic rate limit (middleware)             |
| `service.sms_unavailable`  | 503  | SMS provider unreachable                    |
| `internal.error`           | 500  | Unexpected server error                     |

Later phases add namespaces: `report.*`, `claim.*`, `moderation.*`, etc.

### Phase 04 — Public report access

| Code                 | HTTP | Description                                          |
| -------------------- | ---- | ---------------------------------------------------- |
| `report.not_found`   | 404  | Report missing, wrong type URL, or non-public status |
| `report.unavailable` | 404  | Terminal status — Angular routes to `/unavailable`   |
