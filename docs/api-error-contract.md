# API Error Contract

Flat envelope: `{ code, message, errors? }`. English in API; Angular localizes via `code`. Implemented in [Phase 01](./PHASE-01-platform-foundation.md).

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

**400 validation** - `POST /api/auth/register`

```json
{
  "code": "validation.failed",
  "message": "Please correct the errors in the form.",
  "errors": { "displayName": ["Display name is required."] }
}
```

**401** - `GET /api/auth/me`

```json
{ "code": "auth.unauthorized", "message": "Authentication required." }
```

**403 banned** - `POST /api/auth/login`

```json
{
  "code": "auth.banned",
  "message": "Your account has been banned: repeated policy violations."
}
```

**429 cooldown** - `POST /api/auth/otp/send` + header `Retry-After: 87`

```json
{
  "code": "otp.cooldown",
  "message": "Please wait 87 seconds before requesting a new code."
}
```

**503 SMS outage** - `POST /api/auth/otp/send`

```json
{
  "code": "service.sms_unavailable",
  "message": "SMS delivery is temporarily unavailable. Please try again later."
}
```

**500**

```json
{ "code": "internal.error", "message": "An unexpected error occurred." }
```

No stack traces, phone numbers, or OTP codes in error bodies.
