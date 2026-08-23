# Phase 01 - Platform Foundation

**Status:** In progress  
**Prerequisites:** None (first phase)

## Progress

| Step                                               | Done          |
| -------------------------------------------------- | ------------- |
| Decisions & API error contract                     | Yes           |
| Monorepo scaffold (API + Angular + Postgres)       | Yes           |
| API plumbing (errors, rate limit, tests)           | Yes           |
| Auth DB (EF Core entities + migration)             | No - **next** |
| Utilities, OTP, sessions, schema/seeds, UI, deploy | No            |

---

## 1. Summary

Establish the runnable monorepo on Railway: Angular PWA (Arabic RTL), ASP.NET Core API, PostgreSQL, and object storage. Ship the full database schema via EF Core migrations, phone OTP authentication with JWT sessions, admin bootstrap, seed data (categories and governorates), static legal/support pages, and shared foundations (timezone helpers, text normalization, rate-limit response pattern). When complete, signup/login/logout flows are testable end-to-end with a real SMS provider.

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
| Section 21   | Infrastructure (Railway deploy, EF migrations, production-only env)             |

**Part II (technical):** Section 16, Section 17, Section 18, Section 20.2, Section 21

---

## 3. Prerequisites

### Prior phases

- None

### Deferred decisions (Section 14)

Resolve **before starting** this phase:

| Item                        | Notes                                                                                   |
| --------------------------- | --------------------------------------------------------------------------------------- |
| OTP / SMS provider          | Real provider required; implement behind `ISmsSender` abstraction                       |
| API error contract appendix | Define standard error shape (`code`, `message`, field errors) before any endpoints ship |

---

## 4. Deliverables

### API

| Method | Route                         | Purpose                                                                 |
| ------ | ----------------------------- | ----------------------------------------------------------------------- |
| POST   | `/api/auth/otp/send`          | Send OTP after bot check + send-limit validation                        |
| POST   | `/api/auth/otp/verify`        | Verify code; returns provisional token if new user                      |
| POST   | `/api/auth/register`          | Complete signup: display name + ToS acceptance -> create `User`        |
| POST   | `/api/auth/login`             | Login for returning users (after OTP verify)                            |
| POST   | `/api/auth/refresh`           | Rotate refresh token; issue new access token                            |
| POST   | `/api/auth/logout`            | Revoke current refresh token                                            |
| POST   | `/api/auth/logout-everywhere` | Revoke all refresh tokens for user                                      |
| GET    | `/api/auth/me`                | Current user profile (display name, role; never expose phone to others) |

### UI routes

| Route      | Access | Purpose                                    |
| ---------- | ------ | ------------------------------------------ |
| `/`        | Public | Landing / placeholder home                 |
| `/login`   | Public | Phone + OTP signup/login flow              |
| `/terms`   | Public | Terms of Service (static)                  |
| `/privacy` | Public | Privacy Policy (static)                    |
| `/safety`  | Public | Safety guidance (static)                   |
| `/support` | Public | Support contact email (static)             |
| `/admin`   | Admin  | Shell with role guard (empty dashboard OK) |

### Database

- **Engine:** PostgreSQL 16+ everywhere (dev, tests, production). EF Core with `Npgsql.EntityFrameworkCore.PostgreSQL`.
- **Local dev:** native PostgreSQL on Windows (`localhost:5432`, db `amanah` — see root `README.md`).
- **Integration tests:** PostgreSQL via Testcontainers (`postgres:16`); `ApiWebApplicationFactory` injects `ConnectionStrings:Default` from the container.
- **Production:** Railway managed PostgreSQL; migrations on deploy.
- EF Core migration creating all Section 17 entities: `User`, `Category`, `CategoryFieldDefinition`, `Governorate`, `Report`, `CategoryField`, `ReportPhoto`, `Claim`, `Resolution`, `ChatThread`, `Message`, `Notification`, `OtpCode`, `RefreshToken`, `AbuseReport`, `ModerationAction`
- Seed migration: 8 default categories + field definitions (Section 5.2), 27 governorates
- Admin user seeded from `ADMIN_PHONE` environment variable at deploy
- Indexes on `User.normalizedPhone`, `OtpCode.phone`, `RefreshToken.userId`

### Infrastructure

- Railway project: PostgreSQL service, API service, static Angular build
- Railway Buckets: public and private bucket/prefix wiring (no upload endpoints yet)
- EF Core migrations run on deploy
- Environment variables documented: DB connection, JWT secrets, SMS credentials, `ADMIN_PHONE`, bucket credentials

### Shared utilities

- `Africa/Cairo` date helpers: day boundaries for quotas, `date` field validation
- Text normalization: trim, collapse repeated spaces (used by validation in later phases)
- Arabic search normalization utility (built now; used in Phase 02 write, Phase 04 read)
- Global exception -> API error contract mapper
- Rate-limit middleware returning HTTP 429 with `Retry-After`
- JWT auth middleware; role-based authorization (`User`, `Admin`)
- PWA manifest + service worker shell (Arabic RTL layout, footer with legal links)

---

## 5. Permissions (Section 9)

Server-enforce these matrix rows before marking this phase done:

| Data          | Roles granted access this phase                                                                                              |
| ------------- | ---------------------------------------------------------------------------------------------------------------------------- |
| Phone numbers | Own user via `/api/auth/me`; Admin views **own** phone on `/me` only - admin user lookup (other users' phones) is Phase 08 |
| Display name  | Own user (via `/api/auth/me`)                                                                                                |

All other Section 9 rows are N/A until later phases introduce the underlying features.

---

## 6. Notifications (Section 5.7)

| Event | Recipient | Introduced                               |
| ----- | --------- | ---------------------------------------- |
| -   | -       | Notification center deferred to Phase 03 |

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

From [SPEC.md Section 15.7](./SPEC.md#157-authentication-otp).

- [ ] **Successful send:** given a passed bot check and a phone under the send limits, an SMS is sent and the user can complete signup or login with the code
- [ ] **Resend cooldown:** a resend requested less than **120 seconds** after the last send is blocked with a clear wait message and no SMS is sent
- [ ] **Hourly send limit:** after **2** sends for the same phone in the rolling hour, further requests are blocked with a clear limit message and no SMS is sent
- [ ] **Daily send limit:** after **3** sends for the same phone in the rolling day, further requests are blocked with a clear limit message and no SMS is sent
- [ ] **Verification attempt limit:** after **3** failed entries the code is void and a new code must be requested
- [ ] **Account creation point:** abandoning signup after OTP verification but before submitting a display name and accepting the Terms leaves no account, and the same phone can start signup again
- [ ] **Banned sign-in:** a banned user's sign-in is refused with the recorded ban reason
- [ ] **Provider outage:** when the verification service is unavailable, signup/login is blocked with a clear temporary-unavailable message and no access is granted

**Additional phase gate:**

- [ ] Admin role guard blocks non-admin access to `/admin`
- [ ] JWT access token (15 min) + refresh token (30 days) with rotation on refresh
- [ ] Logout everywhere revokes all refresh tokens
- [ ] Seed data: 8 categories with field defs, 27 governorates present after migration

---

## 9. Definition of done

### Automated tests

- [ ] OTP send limits (cooldown, hourly, daily) with no SMS sent when blocked
- [ ] OTP verification: success, 3-attempt void, expired code rejection
- [ ] Account created only after display name + ToS; abandoned signup leaves no `User` row
- [ ] JWT issue, refresh rotation, logout, logout-everywhere
- [ ] Banned user rejected at login and token refresh
- [ ] Admin bootstrap: seeded phone has `Admin` role
- [ ] API error contract shape on validation and auth failures

### Manual smoke checklist

- [ ] Deploy to Railway; migrations apply cleanly
- [ ] Receive real SMS OTP on Egyptian mobile number
- [ ] Complete signup with Arabic display name; RTL layout renders correctly
- [ ] Terms, Privacy, Safety, Support pages reachable from footer (logged out)
- [ ] Non-admin user redirected/blocked from `/admin`
- [ ] Bot check blocks OTP send when failed

### Phase exit gate

This phase is complete when all acceptance criteria pass and no out-of-scope items were implemented early.
