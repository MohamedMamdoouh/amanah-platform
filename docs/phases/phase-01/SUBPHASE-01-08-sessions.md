# Sub-phase 08 — Sessions & Identity

**Status:** Not started  
**Prerequisites:** [Sub-phase 07 — OTP Verify](./SUBPHASE-01-07-otp-verify.md)

---

## 1. Summary

Implement the remaining auth API endpoints: register, login, refresh, logout, logout-everywhere, and `/me`. Issue JWT access tokens (15 min) and refresh tokens (30 days) with rotation. Enforce ban checks and the account-creation rule (no `User` row until display name + ToS).

---

## 2. SPEC references

| SPEC section | Topic |
| ------------ | ----- |
| Section 5.1 | Display name rules, ToS acceptance, sessions, ban, roles |
| Section 9 | Phone numbers: own user + admin only |
| Section 15.7 | Account creation point; banned sign-in |
| Section 18 | JWT + refresh token implementation |

**Contract reference:** [docs/api-error-contract.md](../../api-error-contract.md)

---

## 3. What you will learn

- JWT access token structure (claims: `sub`, `role`, `jti`)
- Opaque refresh token generation, hashing, and rotation on each refresh
- ASP.NET Core JWT bearer authentication and authorization policies
- Why "logout everywhere" revokes DB refresh tokens but access tokens expire naturally within 15 min
- Permission enforcement: phone visible on `/me` only to the authenticated user (admin sees own phone on `/me` too)

**Files to read after implementing:**

- `api/Services/Auth/TokenService.cs`
- `api/Services/Auth/AuthService.cs`
- `api/Controllers/AuthController.cs` (all actions)
- `api/Program.cs` (JWT bearer configuration)

---

## 4. Deliverables

### Endpoints

| Method | Route | Auth | Purpose |
| ------ | ----- | ---- | ------- |
| POST | `/api/auth/register` | Provisional token | Create `User` with display name + ToS |
| POST | `/api/auth/login` | None (post-OTP) | Issue tokens for returning user |
| POST | `/api/auth/refresh` | Refresh token body | Rotate refresh token; issue new access token |
| POST | `/api/auth/logout` | Bearer + refresh body | Revoke current refresh token |
| POST | `/api/auth/logout-everywhere` | Bearer | Revoke all refresh tokens for user |
| GET | `/api/auth/me` | Bearer | Current user profile |

### `POST /api/auth/register`

**Request:**

```json
{
  "provisionalToken": "...",
  "displayName": "أحمد",
  "acceptTerms": true,
  "termsVersion": "1.0"
}
```

**Validation:**

| Field | Rules |
| ----- | ----- |
| `provisionalToken` | Valid signup JWT from verify |
| `displayName` | 3–40 chars; Arabic/Latin letters, digits, spaces, `- _ .` |
| `acceptTerms` | Must be `true` |
| `termsVersion` | Must match `CURRENT_TERMS_VERSION` from configuration (e.g. `appsettings.json` / env `CURRENT_TERMS_VERSION`, default `"1.0"` — same string referenced on the static `/terms` page) |

**Behavior:**

- Create `User` row with normalized phone from token, display name, role `User`, ToS fields
- Issue access + refresh tokens
- Return token pair + user profile

**Abandoned signup:** If user verified OTP but never calls register, no `User` row exists. Same phone can restart signup.

### `POST /api/auth/login`

**Request** (returning user — after successful `otp/verify` with `existing_user`):

```json
{
  "phone": "01XXXXXXXXX",
  "verifyToken": "<short-lived token from verify response>"
}
```

**Do not** accept the raw OTP `code` here — sub-phase 07 marks the code used/deleted on verify. Login exchanges the verify-issued token for access/refresh tokens.

**Behavior:**

- User must exist
- Ban check → `403 auth.banned` with reason
- Issue access + refresh tokens

### Token configuration

| Token | Lifetime | Storage |
| ----- | -------- | ------- |
| Access (JWT) | 15 minutes | Client memory / secure storage |
| Refresh (opaque) | 30 days | Client secure storage; hash in `RefreshToken` table |

**Refresh rotation:** Each `POST /api/auth/refresh` revokes the presented refresh token and issues a new pair. Reuse of revoked token → `401 auth.refresh_invalid`.

### `GET /api/auth/me`

**Response:**

```json
{
  "id": "uuid",
  "displayName": "أحمد",
  "role": "User",
  "phone": "+201012345678"
}
```

Phone is included because caller is always the own user (or admin viewing self). Phone is **never** returned in any endpoint that could expose another user's data (no such endpoints in Phase 01).

### Ban enforcement

- Checked on `login` and `refresh`
- Banned user → `403 auth.banned` with `message` containing ban reason

### Authorization policies

| Policy | Requirement |
| ------ | ----------- |
| `Authenticated` | Valid access JWT |
| `Admin` | Role claim = `Admin` |

---

## 5. Step-by-step implementation order

1. Configure JWT bearer authentication in `Program.cs`
2. Implement `TokenService` (issue access, issue refresh, validate, revoke)
3. Implement `AuthService.RegisterAsync`, `LoginAsync`, `RefreshAsync`, `LogoutAsync`, `LogoutEverywhereAsync`, `GetMeAsync`
4. Complete `AuthController` with all actions
5. Add `[Authorize]` attributes
6. Write full auth integration test suite
7. Test abandoned signup: verify → no register → no `User` row → verify again succeeds

---

## 6. Out of scope

- Admin user seed (sub-phase 09)
- Angular auth UI (sub-phase 11)
- Railway deploy (sub-phase 12)

---

## 7. Validation gate

### Automated tests

- [ ] Register with valid provisional token → `User` created; tokens returned
- [ ] Register without provisional token → `400 auth.provisional_invalid`
- [ ] Register with `acceptTerms: false` → `400 validation.failed`
- [ ] Abandoned signup: verify → no register → no `User` row; same phone can verify again
- [ ] Login existing user → tokens returned
- [ ] Login banned user → `403 auth.banned` with reason
- [ ] Refresh with valid token → new token pair; old refresh revoked
- [ ] Refresh with revoked token → `401 auth.refresh_invalid`
- [ ] Refresh banned user → `403 auth.banned`
- [ ] Logout → refresh token revoked; subsequent refresh fails
- [ ] Logout-everywhere → all refresh tokens for user revoked
- [ ] `/me` returns display name, role, phone for authenticated user
- [ ] `/me` without token → `401 auth.unauthorized`
- [ ] API error contract shape on all failure paths

### Manual smoke checklist

- [ ] Full API flow via curl/Postman: send → verify → register → me → refresh → logout
- [ ] Access token expires after 15 min (or test with shortened lifetime in dev config)

---

## 8. Exit criteria

- [ ] All automated tests pass
- [ ] All auth endpoints from Phase 01 deliverables implemented
- [ ] Mark sub-phase 08 complete in [phase-01/README.md](./README.md)
