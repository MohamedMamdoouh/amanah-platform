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
| 401    | Missing / invalid token          | `auth.unauthorized`                       |
| 403    | Wrong role, banned, or deactivated | `auth.forbidden`, `auth.banned`, `account.reactivation_required` |
| 404    | Not found or no visibility       | `resource.not_found`                      |
| 409    | State conflict                   | `resource.conflict`                       |
| 410    | Permanently unavailable public listing | `resource.unavailable`               |
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
| `auth.token_expired`       | —    | Defined in `ErrorCodes`; expired bearer tokens are rejected by JWT middleware and do not emit this code |
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

Phase 02 moderation failures use shared `resource.conflict` / `resource.not_found` (no `moderation.*` namespace). Phase 04 adds `claim.*` (see below).

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

Constants defined in `ErrorCodes` and not emitted by current validators: `field.signup_token.required`, `field.reset_token.required`, `field.refresh_token.required`, `field.password.invalid`, `field.otp_purpose.invalid`, `field.otp_code.invalid`, `resource.not_implemented`.

### Account (`account.*`)

| Code | HTTP | When |
| ---- | ---- | ---- |
| `account.deactivation_blocked` | 409 | Deactivation refused; `errors.blockers` lists `claim_in_progress` and/or `approved_claim` |
| `account.deactivation_already_requested` | 409 | Account is already deactivated |
| `account.reactivation_required` | 403 | Deactivated account called an active-account API |

`GET /api/v1/account/deactivation-status` returns `{ canDeactivate, blockers, deactivatedAt }`. Blocker ids are `claim_in_progress` and `approved_claim`. `account.deactivated` is defined in `ErrorCodes` and is not returned.

### Chat

| Code | HTTP | When |
| ---- | ---- | ---- |
| `chat.read_only` | 409 | Send attempted on a read-only thread (REST and SignalR) |

---

## Error codes - Phase 01 (report submission)

### Report (`report.*`)

| Code | HTTP | When | `errors` map |
| ---- | ---- | ---- | ------------ |
| `report.daily_quota` | 429 | 3+ new reports in the current Cairo day | No — summary only; `Retry-After` until next Cairo midnight |
| `report.open_cap` | 429 | 5 open reports (`pending_review`, `published`, `claim_in_progress`) | No — summary only |
| `report.contact_info` | — | Reserved; contact-info violations use `validation.failed` with per-field messages | Yes |

Report create/validation also returns `validation.failed` (400) with field keys: `type`, `categoryCode`, `title`, `description`, `dateLostOrFound`, `governorateCode`, `areaText`, `heldLocation`, `rewardAmount`, category field keys, and `photos[n]`.

### Upload (`upload.*`)

| Code | HTTP | When | `errors` map |
| ---- | ---- | ---- | ------------ |
| `upload.invalid_format` | 400 | Wrong MIME or content-type mismatch on report photo | Often on `photos[n]` |
| `upload.too_large` | 400 | File exceeds 5 MB | Often on `photos[n]` |
| `upload.storage_failed` | 503 | R2 put failure during report submit | No |

`POST /api/v1/reports` (multipart with photos) may return `rate_limit.exceeded` (429) from the `photo-upload` middleware policy (5/min + 20/hour per user when `photo-upload-hourly` is configured).

Claim submit uses the same `upload.*` codes on the `photo` field when an optional photo part is present (see Phase 04).

---

## Error codes - Phase 02 (moderation)

| Code | HTTP | When | `errors` map |
| ---- | ---- | ---- | ------------ |
| `report.resubmit_cap` | 409 | 3rd resubmission already rejected | No — summary only |

---

## Error codes - Phase 04 (claims)

### Claim (`claim.*`)

| Code | HTTP | When | `errors` map |
| ---- | ---- | ---- | ------------ |
| `claim.daily_quota` | 429 | 5+ claim submissions in the current Cairo day | No — summary only; `Retry-After` until next Cairo midnight |
| `claim.attempt_limit` | 429 | 3 counted failures on the same report | No — summary only |
| `claim.pending_exists` | 409 | User already has an open `Pending` claim on this report | No |
| `claim.own_report` | 409 | Claimant is the report owner | No |
| `claim.invalid_status` | 409 | Report is not `Published` (no attempt consumed) | No |

Claim submit validation also returns `validation.failed` (400) with field keys: `submittedAnswer` (length, contact-info block). Direction-specific prompt enforcement is frontend-only in Phase 04.

### Claim review and reads

| Method | Route | Success | Notes |
| ------ | ----- | ------- | ----- |
| GET | `/api/v1/reports/{id}/claims` | 200 `ReportClaimSummaryResponse[]` | Reporter only |
| GET | `/api/v1/claims/mine` | 200 paginated `MyClaimSummaryResponse` | Claimant only |
| GET | `/api/v1/claims/{id}` | 200 `ClaimDetailResponse` | Claimant or reporter |
| POST | `/api/v1/claims/{id}/approve` | 204 | Reporter; report → `claim_in_progress` |
| POST | `/api/v1/claims/{id}/reject` | 204 | Reporter; counts as failure |
| POST | `/api/v1/claims/{id}/withdraw` | 204 | Claimant; pending only |

Claim notifications: `NewClaimSubmitted`, `ClaimApproved`, `ClaimRejected`, `ClaimWithdrawnByClaimant`, `ClaimClosedReportUnavailable` (see `NotificationTypes.cs`).

### Claim photo (multipart)

`POST /api/v1/reports/{id}/claims` accepts `multipart/form-data`:

| Part | Required | Content |
| ---- | -------- | ------- |
| `claim` | Yes | JSON `{ "submittedAnswer": "..." }` |
| `photo` | No | Single image file (max one part; same rules as report photos) |

- Invalid or oversized photo → **400** with `upload.*` or `validation.failed` on `photo`; **no claim row created**
- More than one `photo` part → **400** `validation.failed` on `photo`
- When a photo part is present, `rate_limit.exceeded` (429) may apply via the `photo-upload` policy (same as report create)

`GET /api/v1/uploads/claim-photo/{claimId}/url` returns a 5-minute presigned URL for claimant or reporter. Admin access requires an **open abuse flag** on the listing (`403` with `abuse.investigation_unavailable` when investigation is closed).

`GET /api/v1/uploads/report-photo/{photoId}/url` for private category photos: reporter always; admin on `Pending Review` / `Rejected` for moderation, or on `Published` / `Claim In Progress` only during an open investigation (same error when blocked).

Non-reporters (admin) may load detail only for statuses in the server allowlist (`pending_review`, `rejected`, `withdrawn`).

---

## Error codes - Phase 07 (abuse and enforcement)

### Abuse (`abuse.*`)

| Code | HTTP | When | `errors` map |
| ---- | ---- | ---- | ------------ |
| `abuse.duplicate_flag` | 409 | User already has an open flag on this listing | No |
| `abuse.invalid_reason` | 400 | Flag reason not in predefined set | Yes — `reason` |
| `abuse.cannot_flag_own_listing` | 409 | Listing owner attempted to flag | No |
| `abuse.listing_not_flaggable` | 409 | Report not in a flaggable status | No |
| `abuse.already_resolved` | 409 | Abuse report already resolved | No |
| `abuse.not_open` | — | Defined in `ErrorCodes`; resolve of a non-open flag returns `abuse.already_resolved` | No |
| `abuse.invalid_outcome` | 400 | Unknown resolve outcome | Yes — `outcome` |
| `abuse.investigation_unavailable` | 403 | Admin investigation or presign without open flag | No |

### Enforcement (`enforcement.*`)

| Code | HTTP | When | `errors` map |
| ---- | ---- | ---- | ------------ |
| `enforcement.user_already_banned` | 409 | Ban on already-banned user | No |
| `enforcement.user_not_banned` | 409 | Unban on non-banned user | No |
| `enforcement.report_not_takedownable` | 409 | Takedown on wrong report status | No |

---

## Success response conventions

| Endpoint pattern | Status | Body |
| ---------------- | ------ | ---- |
| `POST /api/v1/reports` | 200 | `{ id, status }` |
| `POST /api/v1/reports/{id}/claims` | 200 | `{ id, status }` |
| `POST /api/v1/auth/otp/send` | 204 | — |
| Approve, reject, resubmit, withdraw, update, mark-read | 204 | — |
| `POST /api/v1/reports/{id}/flag` | 200 | Flag body |
| `POST /api/v1/admin/abuse/{id}/resolve`, takedown, ban, unban | 200 | Action result JSON |
| List/detail GET endpoints | 200 | Resource JSON |

Public detail for `Resolved`, `Withdrawn`, and `Removed by Admin` returns **410** with `resource.unavailable` (`GET /api/v1/reports/{id}/public`, `/api/v1/lost/{id}`, `/api/v1/found/{id}`).

### Pagination

| Endpoint | Query | Defaults |
| -------- | ----- | -------- |
| `GET /api/v1/reports` | `page`, `pageSize` (also `q`, `category`, `governorate`, `type`, `dateFrom`, `dateTo`) | page 1, size 20, max 50 |
| `GET /api/v1/claims/mine` | `page`, `pageSize` | page 1, size 20, max 50 |
| `GET /api/v1/chats/{threadId}` | `before` (message id), `limit` | limit 50, max 100 |

Paginated JSON uses `items`, `page`, `pageSize`, `totalCount`, `totalPages`. `GET /api/v1/notifications` returns the caller's list without paging.

---

## Frontend error handling

Angular maps `code` → `error.{code}` in `web/src/assets/i18n/ar/errors.json` via `ApiErrorService` (`web/src/app/i18n/api-error.service.ts`):

- `summary(error)` — translated summary, falling back to `message`
- `fieldErrors(error)` — `errors` map for inline form display

Report UI uses `web/src/assets/i18n/ar/reports.json`; claim UI uses `claims.json` and `notifications.json` for claim event labels.

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

**429 claim quota** - `POST /api/v1/reports/{id}/claims` + header `Retry-After: <seconds>`

```json
{
  "code": "claim.daily_quota",
  "message": "You have reached the daily limit of 5 claims. Try again after midnight (Cairo time)."
}
```

**400 claim photo** - `POST /api/v1/reports/{id}/claims` (multipart, `photo` part)

```json
{
  "code": "upload.invalid_format",
  "message": "Unsupported image format.",
  "errors": { "photo": ["Unsupported image format."] }
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
| Refresh token (30 days) | `Set-Cookie` `amanah_refresh` (`HttpOnly`, `SameSite=Lax`, `Secure` outside Development, `Path=/api/v1/auth`) | Browser cookie jar — not readable by JS |

- `POST /api/v1/auth/refresh` — no body; refresh cookie sent automatically (`withCredentials: true` on web client).
- `POST /api/v1/auth/logout` — no body; revokes cookie token and clears cookie.
- `AuthSessionResponse` JSON: `{ accessToken, user }` only — no `refreshToken` field.
- CORS: `AllowCredentials` with explicit `Cors:AllowedOrigins` (e.g. `http://localhost:4200` in Development).
