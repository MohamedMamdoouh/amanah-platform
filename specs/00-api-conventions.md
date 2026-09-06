# API Conventions

Flat envelope: `{ code, message, errors? }`. English in API; Angular localizes via `code`. Implemented in platform foundation.

**Base path:** `/api/v1/...` (URL versioning via `Asp.Versioning.Mvc`).

```json
{
  "code": "validation.failed",
  "message": "Please correct the errors in the form.",
  "errors": { "displayName": ["Display name is required."] }
}
```

| Field     | Required | Description                                   |
| --------- | -------- | --------------------------------------------- |
| `code`    | Yes      | Stable identifier for i18n / branching        |
| `message` | Yes      | English summary for this occurrence           |
| `errors`  | No       | camelCase field keys → English message arrays |

**Content-Type:** `application/json`. Success responses are not wrapped.

---

## HTTP status mapping

| Status | When                             | Code examples                             |
| ------ | -------------------------------- | ----------------------------------------- |
| 400    | Validation, client-fixable rules | `validation.*`, `auth.*`                  |
| 401    | Missing / invalid token          | `auth.unauthorized`, `auth.token_expired` |
| 403    | Wrong role or banned             | `auth.forbidden`, `auth.banned`           |
| 404    | Not found or no visibility       | `resource.not_found`                      |
| 409    | State conflict                   | `resource.conflict`                       |
| 429    | Rate limit or quota              | `rate_limit.*`, `otp.*`, `report.*`       |
| 503    | External dependency down         | `service.*`, `upload.storage_failed`      |
| 500    | Unexpected fault                 | `internal.error`                          |

---

## `Retry-After` (429)

`Retry-After` (seconds) is included when a retry window is known. Required for OTP limits (`otp.cooldown`, `otp.hourly_limit`, `otp.daily_limit`) and `report.daily_quota`. May be absent for `report.open_cap` and generic `rate_limit.exceeded` middleware responses. OTP limits: no SMS sent when blocked.

---

## Error codes - Platform foundation (auth)

| Code                       | HTTP | Description                   |
| -------------------------- | ---- | ----------------------------- |
| `validation.failed`        | 400  | Field validation failed       |
| `auth.invalid_phone`       | 400  | Phone format not accepted     |
| `auth.captcha_failed`      | 400  | CAPTCHA failed                |
| `auth.invalid_otp`         | 400  | OTP incorrect                 |
| `auth.otp_expired`         | 400  | OTP expired                   |
| `auth.otp_void`            | 400  | OTP voided (3 failures)       |
| `auth.handoff_token_invalid` | 400  | Signup/reset handoff token invalid |
| `auth.invalid_credentials` | 400  | Wrong phone or password on login |
| `auth.account_exists`        | 409  | Signup OTP requested for existing phone |
| `auth.unauthorized`        | 401  | No valid access token         |
| `auth.token_expired`       | 401  | Access token expired          |
| `auth.refresh_invalid`     | 401  | Refresh token invalid/revoked |
| `auth.banned`              | 403  | Account banned                |
| `auth.forbidden`           | 403  | Insufficient permission       |
| `otp.cooldown`             | 429  | 120s resend cooldown          |
| `otp.hourly_limit`         | 429  | 2 sends / rolling hour        |
| `otp.daily_limit`          | 429  | 3 sends / Cairo day           |
| `rate_limit.exceeded`      | 429  | Generic middleware limit (incl. login) |
| `service.sms_unavailable`  | 503  | SMS provider down             |
| `internal.error`           | 500  | Unexpected error              |

Shared across phases (not repeated in phase tables):

| Code | HTTP | Description |
| ---- | ---- | ----------- |
| `resource.not_found` | 404 | Entity missing or caller lacks visibility (same response either way) |
| `resource.conflict` | 409 | Invalid state transition (e.g. withdraw non-pending report) |

Phase 02 moderation failures use shared `resource.conflict` / `resource.not_found` (no `moderation.*` namespace). Later phases add `claim.*`, etc.

### Field validation (`field.*`)

Used by auth validators; returned inside `validation.failed` or as field keys in `errors`:

| Code | HTTP | Description |
| ---- | ---- | ----------- |
| `field.phone.required` | 400 | Phone required |
| `field.phone.invalid` | 400 | Phone format not accepted |
| `field.display_name.required` | 400 | Display name required |
| `field.display_name.invalid` | 400 | Display name format invalid |
| `field.password.required` | 400 | Password required |
| `field.password.too_short` | 400 | Password under 8 characters |
| `field.accept_terms.required` | 400 | Terms acceptance required |
| `field.captcha_token.required` | 400 | CAPTCHA token required |
| `field.otp_code.required` | 400 | OTP code required |
| `field.otp_purpose.required` | 400 | OTP purpose required |

---

## Error codes - Phase 01 (report submission)

### Report (`report.*`)

| Code | HTTP | When | `errors` map |
| ---- | ---- | ---- | ------------ |
| `report.daily_quota` | 429 | 3+ new reports in the current Cairo day | No — summary only; `Retry-After` until next Cairo midnight |
| `report.open_cap` | 429 | 5 open reports (`pending_review`, `published`, `claim_in_progress`) | No — summary only |
| `report.contact_info` | — | Reserved; contact-info violations use `validation.failed` with per-field messages | Yes |

Report create/validation also returns `validation.failed` (400) with field keys: `type`, `categoryCode`, `title`, `description`, `dateLostOrFound`, `governorateCode`, `areaText`, `heldLocation`, `hiddenDetail`, `rewardAmount`, category field keys, and `photos[n]`.

### Upload (`upload.*`)

| Code | HTTP | When | `errors` map |
| ---- | ---- | ---- | ------------ |
| `upload.invalid_format` | 400 | Wrong MIME or content-type mismatch on report photo | Often on `photos[n]` |
| `upload.too_large` | 400 | File exceeds 5 MB | Often on `photos[n]` |
| `upload.storage_failed` | 503 | R2 put failure during report submit | No |

`POST /api/v1/reports` (multipart with photos) may return `rate_limit.exceeded` (429) from the `photo-upload` middleware policy (5/min + 20/hour per user when `photo-upload-hourly` is configured).

---

## Error codes - Phase 02 (moderation)

| Code | HTTP | When | `errors` map |
| ---- | ---- | ---- | ------------ |
| `report.resubmit_cap` | 409 | 3rd resubmission already rejected | No — summary only |

---

## Success response conventions

| Endpoint pattern | Status | Body |
| ---------------- | ------ | ---- |
| `POST /api/v1/reports` | 200 | `{ id, status }` |
| `POST /api/v1/auth/otp/send` | 204 | — |
| Approve, reject, resubmit, withdraw, update, mark-read | 204 | — |
| List/detail GET endpoints | 200 | Resource JSON |

---

## Frontend error handling

Angular maps `code` → `error.{code}` in `web/src/assets/i18n/ar/errors.json` via `ApiErrorService` (`web/src/app/i18n/api-error.service.ts`):

- `summary(error)` — translated summary, falling back to `message`
- `fieldErrors(error)` — `errors` map for inline form display

Report UI also uses `web/src/assets/i18n/ar/reports.json` for form copy.

---

## Examples

**400 validation** - `POST /api/v1/auth/register`

```json
{
  "code": "validation.failed",
  "message": "Please correct the errors in the form.",
  "errors": { "password": ["Password must be at least 8 characters."] }
}
```

**400 invalid credentials** - `POST /api/v1/auth/login`

```json
{
  "code": "auth.invalid_credentials",
  "message": "Phone number or password is incorrect."
}
```

**409 account exists** - `POST /api/v1/auth/otp/send` (`purpose=signup`)

```json
{
  "code": "auth.account_exists",
  "message": "An account already exists for this phone number. Sign in instead."
}
```

**401** - `GET /api/v1/auth/me`

```json
{ "code": "auth.unauthorized", "message": "Authentication required." }
```

**403 banned** - `POST /api/v1/auth/login`

```json
{
  "code": "auth.banned",
  "message": "Your account has been banned: repeated policy violations."
}
```

**429 cooldown** - `POST /api/v1/auth/otp/send` + header `Retry-After: 87`

```json
{
  "code": "otp.cooldown",
  "message": "Please wait 87 seconds before requesting a new code."
}
```

**503 SMS outage** - `POST /api/v1/auth/otp/send` (CAPTCHA timeout only)

```json
{
  "code": "service.sms_unavailable",
  "message": "Service is temporarily unavailable. Please try again later."
}
```

Used when CAPTCHA verification times out (Turnstile slow/unavailable). SMS provider failures are handled asynchronously by the outbox worker — the API returns **204** after enqueue.

**204 accepted** - `POST /api/v1/auth/otp/send`

The request passed validation and the OTP was enqueued. SMS delivery happens asynchronously via the outbox worker. The user should check their phone; if no SMS arrives within a minute, retry respecting cooldown/limits.

**Residual ambiguity:** If the SMS provider accepts the message but the HTTP response times out, the API returns **503**, keeps the **`otp_codes`** row, and leaves the outbox **`Pending`** for worker retry (idempotency key = outbox Id). The user may still receive the SMS and can try to verify. If the incoming request times out while dispatch continues server-side, the client may see **504** even though the outbox later becomes **`Sent`**.

**429 report quota** - `POST /api/v1/reports` + header `Retry-After: <seconds>`

```json
{
  "code": "report.daily_quota",
  "message": "You have reached the daily limit of 3 new reports. Try again after midnight (Cairo time)."
}
```

**400 upload** - `POST /api/v1/reports` (multipart, photo part)

```json
{
  "code": "upload.too_large",
  "message": "Image must not exceed 5 MB.",
  "errors": { "photos[0]": ["Image must not exceed 5 MB."] }
}
```

**500**

```json
{ "code": "internal.error", "message": "An unexpected error occurred." }
```

No stack traces, phone numbers, or OTP codes in error bodies.

---

## Session tokens (Platform foundation)

| Token | Transport | Client storage |
| ----- | ----------- | -------------- |
| Access token (15 min) | JSON body on `register`, `login`, `refresh` | Angular memory only |
| Refresh token (30 days) | `Set-Cookie` `amanah_refresh` (`HttpOnly`, `SameSite=Lax`, `Path=/api/v1/auth`) | Browser cookie jar — not readable by JS |

- `POST /api/v1/auth/refresh` — no body; refresh cookie sent automatically (`withCredentials: true` on web client).
- `POST /api/v1/auth/logout` — no body; revokes cookie token and clears cookie.
- `AuthSessionResponse` JSON: `{ accessToken, user }` only — no `refreshToken` field.
- CORS: `AllowCredentials` with explicit `Cors:AllowedOrigins` (e.g. `http://localhost:4200` in Development).
