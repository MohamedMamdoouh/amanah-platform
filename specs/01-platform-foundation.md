# Phase 01 - Platform Foundation

**Status:** Complete  
**Prerequisites:** None (first phase)

## Progress

| Step                                                                 | Done                                |
| -------------------------------------------------------------------- | ----------------------------------- |
| Decisions & API error contract                                       | Yes                                 |
| Monorepo scaffold (API + Angular + Postgres)                         | Yes                                 |
| API plumbing (errors, rate limit, tests)                             | Yes                                 |
| Auth DB (EF Core entities + migration)                               | Yes                                 |
| Utilities, OTP, sessions, schema/seeds, UI, deploy                   | Yes                                 |
| Cache foundation (`ICacheService` + `HybridCache`, no consumers yet) | Yes                                 |

---

## 1. Summary

Establish the runnable monorepo on Render (single Docker service for API + Angular), Supabase Postgres, and Cloudflare R2 config: Arabic RTL Angular SPA, ASP.NET Core API, phone OTP authentication with JWT sessions, admin bootstrap, seed data, static legal pages, and shared foundations. When complete, signup/login/logout flows are testable end-to-end with a real SMS provider.

---

## 2. SPEC references

| SPEC section | Topic                                                                           |
| ------------ | ------------------------------------------------------------------------------- |
| Section 3    | Target users, timezone rules (UTC storage, Cairo display, Cairo day boundaries) |
| Section 5.1  | Authentication (OTP, display name, ToS, sessions, ban check, roles)             |
| Section 5.8  | Terms of Service and Privacy Policy pages                                       |
| Section 7.5  | OTP send limits and bot protection                                              |
| Section 9    | Permissions for auth-only surfaces (phone numbers: own + admin)                 |
| Section 11   | Browser support, RTL, accessibility baseline, Cairo time display                |
| Section 12   | OTP and session retention rules (schema + cleanup stub OK; job in Phase 07)     |
| Section 15.7 | Authentication acceptance criteria                                              |
| Section 16   | Architecture and stack                                                          |
| Section 17   | Full data model (all entities migrated; feature tables may be empty)            |
| Section 18   | Authentication and sessions implementation                                      |
| Section 20.2 | HTTP 429 + `Retry-After` rate-limit response pattern                            |
| Section 21   | Infrastructure (production deploy, EF migrations, production-only env)             |

**Part II (technical):** Section 16, Section 17, Section 18, Section 20.2, Section 21

---

## 3. Prerequisites

### Prior phases

- None

### Deferred decisions (Section 14)

Resolve **before starting** this phase:

| Item                        | Notes                                                                                   |
| --------------------------- | --------------------------------------------------------------------------------------- |
| OTP / SMS provider          | **Done** — [Unimtx](https://www.unimtx.com/) via `UnimtxSmsSender` ([deployment.md](../docs/deployment.md)) |
| API error contract appendix | Define standard error shape (`code`, `message`, field errors) before any endpoints ship |

---

## 4. Deliverables

### API

| Method | Route                            | Purpose                                                                 |
| ------ | -------------------------------- | ----------------------------------------------------------------------- |
| POST   | `/api/v1/auth/otp/send`          | Send OTP (`purpose`: `signup` or `password_reset`) after bot check + send-limit validation |
| POST   | `/api/v1/auth/otp/verify`        | Verify code; returns signup or reset handoff token                                         |
| POST   | `/api/v1/auth/register`          | Complete signup: display name + password + ToS acceptance -> create `User`                 |
| POST   | `/api/v1/auth/login`             | Sign in with phone + password                                                              |
| POST   | `/api/v1/auth/password/reset`    | Set new password after OTP verification; revokes all sessions                              |
| POST   | `/api/v1/auth/refresh`           | Rotate refresh token; issue new access token                            |
| POST   | `/api/v1/auth/logout`            | Revoke current refresh token                                            |
| POST   | `/api/v1/auth/logout-everywhere` | Revoke all refresh tokens for user                                      |
| GET    | `/api/v1/auth/me`                | Current user profile (display name, role; never expose phone to others) |

### UI routes

| Route      | Access | Purpose                                    |
| ---------- | ------ | ------------------------------------------ |
| `/`        | Public | Landing page (browse/report CTAs deferred to Phase 02) |
| `/login`   | Public | Sign-in, sign-up (OTP + profile), and forgot-password flow |
| `/terms`   | Public | Terms of Service (static)                  |
| `/privacy` | Public | Privacy Policy (static)                    |
| `/safety`  | Public | Safety guidance (static)                   |
| `/support` | Public | Support contact email (static)             |
| `/admin`   | Admin  | Shell with role guard (empty dashboard OK) |

### Database

- **Engine:** PostgreSQL 16+ everywhere (dev, tests, production). EF Core with `Npgsql.EntityFrameworkCore.PostgreSQL`.
- **Local dev:** native PostgreSQL on Windows (`localhost:5432`, db `amanah` — see root `README.md`).
- **Integration tests:** PostgreSQL via Testcontainers (`postgres:16`); `ApiWebApplicationFactory` injects `ConnectionStrings:Default` from the container.
- **Production:** Supabase managed PostgreSQL; **Session pooler** on Render (IPv4), direct connection for local dev.
- EF Core migration creating all Section 17 entities: `User`, `Category`, `CategoryFieldDefinition`, `Governorate`, `Report`, `CategoryField`, `ReportPhoto`, `Claim`, `Resolution`, `ChatThread`, `Message`, `Notification`, `OtpCode`, `RefreshToken`, `AbuseReport`, `ModerationAction`
- Seed migration: 8 default categories + field definitions (English `code` / `fieldKey` only), 27 governorates (`code` + `sortOrder`); Arabic labels in `web/src/assets/i18n/ar/categories.json` and `governorates.json`
- Admin user seeded from `ADMIN_PHONE` + `ADMIN_PASSWORD` environment variables at deploy
- Indexes on `User.normalizedPhone`, `OtpCode.phone`, `RefreshToken.userId`

### Infrastructure

- Render: one Docker web service (`api/Dockerfile`) serves API + Angular `wwwroot`
- Supabase: managed PostgreSQL (not Render Postgres)
- Cloudflare R2: public and private prefix wiring via `Bucket__*` env vars (no upload endpoints yet)
- EF Core migrations run on API startup
- Environment variables documented in [deployment.md](../docs/deployment.md)

### Shared utilities

- `Africa/Cairo` date helpers: day boundaries for quotas, `date` field validation
- Text normalization: trim, collapse repeated spaces (used by validation in later phases)
- Arabic search normalization utility (built now; used in Phase 02 write, Phase 04 read)
- Global exception -> API error contract mapper
- Rate-limit middleware returning HTTP 429 with `Retry-After`
- JWT auth middleware; role-based authorization (`User`, `Admin`)
- **Cache foundation:** `ICacheService` + `HybridCache` (L1 + L2 memory; fail-open; Debug hit/miss logs). No endpoint consumers until Phase 02. Config: `Cache:CategoriesTtlSeconds`, `GovernoratesTtlSeconds`. Browse not cached in v1.
- Arabic RTL layout + footer with legal links

---

## 5. Permissions (Section 9)

Server-enforce these matrix rows before marking this phase done:

| Data          | Roles granted access this phase                                                                                            |
| ------------- | -------------------------------------------------------------------------------------------------------------------------- |
| Phone numbers | Own user via `/api/v1/auth/me`; Admin views **own** phone on `/me` only - admin user lookup (other users' phones) is Phase 08 |
| Display name  | Own user (via `/api/v1/auth/me`)                                                                                           |

All other Section 9 rows are N/A until later phases introduce the underlying features.

---

## 6. Notifications (Section 5.7)

| Event | Recipient | Introduced                               |
| ----- | --------- | ---------------------------------------- |
| -     | -         | Notification center deferred to Phase 03 |

Auth error messages (OTP limits, ban reason, provider outage) are returned inline in API responses only.

---

## 7. Out of scope

Explicitly deferred to later phases:

- Report submission -> Phase 02
- Admin moderation queue -> Phase 03
- Browse / search -> Phase 04
- Claims -> Phase 05
- Chat and resolution -> Phase 06
- Scheduled retention/expiry jobs -> Phase 07
- Abuse reporting and enforcement -> Phase 08
- In-app notification center -> Phase 03
- Photo upload endpoints -> Phase 02
- Account self-deletion -> Phase 07 (SPEC Section 5.1)

---

## 8. Acceptance criteria

From [SPEC.md Section 15.7](./SPEC.md#157-authentication).

- [x] **Sign-in:** returning users authenticate with phone + password; wrong credentials return generic `auth.invalid_credentials`
- [x] **Sign-up OTP:** given a passed bot check, unregistered phone under send limits, and `purpose=signup`, an SMS is sent and verification returns a signup handoff token
- [x] **Sign-up completion:** after OTP verification the user submits display name + password + Terms acceptance to create the account
- [x] **Password reset:** registered phone + `purpose=password_reset` sends OTP; after verification user sets new password and all sessions are revoked
- [x] **Signup blocked for existing phone:** OTP send with `purpose=signup` for registered phone returns `auth.account_exists` with no SMS
- [x] **Reset opaque for unknown phone:** OTP send with `purpose=password_reset` for unregistered phone returns 204 without SMS
- [x] **Resend cooldown:** a resend requested less than **120 seconds** after the last send is blocked with a clear wait message and no SMS is sent
- [x] **Hourly send limit:** after **2** sends for the same phone in the rolling hour, further requests are blocked with a clear limit message and no SMS is sent
- [x] **Daily send limit:** after **3** sends for the same phone in the rolling day, further requests are blocked with a clear limit message and no SMS is sent
- [x] **Verification attempt limit:** after **3** failed entries the code is void and a new code must be requested
- [x] **Account creation point:** abandoning signup after OTP verification but before submitting display name, password, and Terms leaves no account, and the same phone can start signup again
- [x] **Banned sign-in:** a banned user's sign-in is refused with the recorded ban reason
- [x] **Provider outage:** when the verification service is unavailable, signup/password-reset OTP is blocked with a clear temporary-unavailable message and no access is granted

**Additional phase gate:**

- [x] Admin role guard blocks non-admin access to `/admin`
- [x] JWT access token (15 min) + refresh token (30 days) with rotation on refresh
- [x] Logout everywhere revokes all refresh tokens
- [x] Seed data: 8 categories with field defs, 27 governorates present after migration

---

## 9. Definition of done

### Automated tests

- [x] OTP send limits (cooldown, hourly, daily) with no SMS sent when blocked
- [x] OTP verification: success, 3-attempt void, expired code rejection
- [x] Account created only after display name + ToS; abandoned signup leaves no `User` row
- [x] JWT issue, refresh rotation, logout, logout-everywhere
- [x] Banned user rejected at login and token refresh
- [x] Admin bootstrap: seeded phone has `Admin` role and password from `ADMIN_PASSWORD`
- [x] API error contract shape on validation and auth failures
- [x] `ICacheService` unit tests: get-or-set and remove (`api.Tests/Infrastructure/CacheServiceTests.cs`)

### Manual smoke checklist

- [x] Deploy to production stack; migrations apply cleanly (see [deployment.md](../docs/deployment.md))
- [x] Receive real SMS OTP on Egyptian mobile number
- [x] Complete signup with Arabic display name; RTL layout renders correctly
- [x] Terms, Privacy, Safety, Support pages reachable from footer (logged out)
- [x] Non-admin user redirected/blocked from `/admin`
- [x] Bot check blocks OTP send when failed

### Phase exit gate

This phase is complete when all acceptance criteria pass and no out-of-scope items were implemented early.
