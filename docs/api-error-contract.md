# API Error Contract

Flat envelope: `{ code, message, errors? }`. English in API; Angular localizes via `code`. Implemented in [Phase 01](./PHASE-01-platform-foundation.md).

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
| 404    | Not found or no visibility       | `report.*`                                |
| 409    | State conflict                   | `conflict.*`                              |
| 429    | Rate limit or quota              | `rate_limit.*`, `otp.*`                   |
| 503    | External dependency down         | `service.*`                               |
| 500    | Unexpected fault                 | `internal.error`                          |

---

## `Retry-After` (429)

Every `429` includes `Retry-After` (seconds). OTP limits (`otp.cooldown`, `otp.hourly_limit`, `otp.daily_limit`): no SMS sent when blocked.

---

## Error codes - Phase 01

| Code                       | HTTP | Description                   |
| -------------------------- | ---- | ----------------------------- |
| `validation.failed`        | 400  | Field validation failed       |
| `auth.invalid_phone`       | 400  | Phone format not accepted     |
| `auth.captcha_failed`      | 400  | CAPTCHA failed                |
| `auth.invalid_otp`         | 400  | OTP incorrect                 |
| `auth.otp_expired`         | 400  | OTP expired                   |
| `auth.otp_void`            | 400  | OTP voided (3 failures)       |
| `auth.provisional_invalid` | 400  | Signup token invalid          |
| `auth.unauthorized`        | 401  | No valid access token         |
| `auth.token_expired`       | 401  | Access token expired          |
| `auth.refresh_invalid`     | 401  | Refresh token invalid/revoked |
| `auth.banned`              | 403  | Account banned                |
| `auth.forbidden`           | 403  | Insufficient permission       |
| `otp.cooldown`             | 429  | 120s resend cooldown          |
| `otp.hourly_limit`         | 429  | 2 sends / rolling hour        |
| `otp.daily_limit`          | 429  | 3 sends / Cairo day           |
| `rate_limit.exceeded`      | 429  | Generic middleware limit      |
| `service.sms_unavailable`  | 503  | SMS provider down             |
| `internal.error`           | 500  | Unexpected error              |

Later phases add `report.*`, `claim.*`, `moderation.*`, etc.

---

## Examples

**400 validation** - `POST /api/v1/auth/register`

```json
{
  "code": "validation.failed",
  "message": "Please correct the errors in the form.",
  "errors": { "displayName": ["Display name is required."] }
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

**500**

```json
{ "code": "internal.error", "message": "An unexpected error occurred." }
```

No stack traces, phone numbers, or OTP codes in error bodies.
