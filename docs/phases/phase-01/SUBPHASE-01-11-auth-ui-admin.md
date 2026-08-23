# Sub-phase 11 — Auth UI & Admin Shell

**Status:** Not started  
**Prerequisites:** [Sub-phase 08 — Sessions & Identity](./SUBPHASE-01-08-sessions.md), [Sub-phase 10 — Angular Shell](./SUBPHASE-01-10-angular-shell.md)

---

## 1. Summary

Wire the Angular frontend to the completed auth API: multi-step login/signup flow, auth state management, JWT interceptor with refresh, and an admin dashboard shell behind a role guard.

---

## 2. SPEC references

| SPEC section | Topic |
| ------------ | ----- |
| Section 5.1 | Phone + OTP signup/login flow, display name, ToS |
| Section 7.5 | Bot check before OTP send (Turnstile widget) |
| Section 9 | Admin role enforcement |
| Section 15.7 | Full authentication acceptance criteria (local E2E) |

**Contract reference:** [docs/api-error-contract.md](../../api-error-contract.md)

---

## 3. What you will learn

- Multi-step reactive forms (phone → OTP → profile/ToS)
- Auth state service with token storage and refresh logic
- Angular route guards (`CanActivateFn`) for admin role
- Displaying API error `message` and field `errors` in forms
- Cloudflare Turnstile widget integration in Angular

**Files to read after implementing:**

- `web/src/app/auth/` — login components and auth service
- `web/src/app/auth/auth.guard.ts`, `admin.guard.ts`
- `web/src/app/auth/auth.interceptor.ts` (completed)
- `web/src/app/admin/` — admin shell component

---

## 4. Deliverables

### Routes

| Route | Component | Guard |
| ----- | --------- | ----- |
| `/login` | `LoginComponent` | Public (redirect to `/` if already logged in) |
| `/admin` | `AdminShellComponent` | `adminGuard` — requires `Admin` role |

### Login flow (`/login`)

Multi-step wizard:

| Step | UI | API call |
| ---- | -- | -------- |
| 1. Phone | Phone input + Turnstile widget | `POST /api/auth/otp/send` |
| 2. OTP | 6-digit code input + resend button (respects cooldown from error) | `POST /api/auth/otp/verify` |
| 3a. New user | Display name + ToS/Privacy checkbox + links to `/terms` and `/privacy` | `POST /api/auth/register` |
| 3b. Returning user | Auto-login after verify (exchange `verifyToken`) | `POST /api/auth/login` |

**Error handling:**

- Display API `message` for all errors
- Bind `errors` object to form fields for validation failures
- Show `Retry-After` wait time for `429` responses (countdown timer on resend button)
- Turnstile reset on `auth.captcha_failed`

### Auth state service (`AuthService`)

| Responsibility | Detail |
| -------------- | ------ |
| Token storage | Access token in memory; refresh token in `sessionStorage` or `localStorage` (document choice) |
| `isLoggedIn()` | Check access token validity |
| `currentUser()` | Signal/observable from `/api/auth/me` |
| `login()` / `register()` / `logout()` | Wrap API calls; update state |
| `refreshToken()` | Called by interceptor on `401 auth.token_expired` |

### HTTP interceptor (complete)

- Attach `Authorization: Bearer <accessToken>` to API requests
- On `401` with `auth.token_expired`: call refresh; retry original request once
- On refresh failure: clear state; redirect to `/login`

### Admin shell (`/admin`)

- Empty dashboard with Arabic heading "لوحة الإدارة"
- `adminGuard`: check JWT role claim === `Admin`; else redirect to `/` or show forbidden page
- Non-admin user navigating to `/admin` is blocked

### Header updates

- Logged out: show "تسجيل الدخول" link to `/login`
- Logged in: show display name + logout button
- Admin users: show link to `/admin`

---

## 5. Step-by-step implementation order

1. Create `AuthService` with API client methods
2. Complete `authInterceptor` with token attach and refresh
3. Build `LoginComponent` step 1 (phone + Turnstile)
4. Build step 2 (OTP entry + resend with cooldown)
5. Build step 3a (register form) and 3b (auto-login)
6. Create `authGuard` and `adminGuard`
7. Create `AdminShellComponent` at `/admin`
8. Update header with auth state
9. Manual E2E smoke test locally

---

## 6. Out of scope

- Real SMS (still `ConsoleSmsSender` locally)
- Railway deployment (sub-phase 12)
- Admin moderation features (Phase 03)
- Notification center (Phase 03)

---

## 7. Validation gate

### Manual E2E smoke checklist (local, ConsoleSmsSender)

- [ ] New user: phone → OTP (from console) → display name + ToS → logged in; `/me` shows profile
- [ ] Logout → redirected; protected state cleared
- [ ] Returning user: phone → OTP → logged in without display name step
- [ ] Page reload while logged in → session restored via refresh token
- [ ] Resend within 120s → error message with wait time; no new OTP in console
- [ ] Wrong OTP 3 times → void message; must request new code
- [ ] Turnstile failure → captcha error shown; no OTP sent
- [ ] Non-admin user blocked from `/admin`
- [ ] Admin user (seeded in sub-phase 09) can access `/admin`
- [ ] Arabic display name renders correctly in RTL layout
- [ ] Footer legal links still work when logged in

### Automated checks

- [ ] `ng build` succeeds
- [ ] `ng test` passes (unit tests for `AuthService` and guards if added)

---

## 8. Exit criteria

- [ ] All manual E2E smoke items pass locally
- [ ] Auth UI routes match Phase 01 deliverables
- [ ] Mark sub-phase 11 complete in [phase-01/README.md](./README.md)
