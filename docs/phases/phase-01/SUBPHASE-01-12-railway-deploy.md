# Sub-phase 12 — Railway Deploy & Production Verification

**Status:** Not started  
**Prerequisites:** [Sub-phase 11 — Auth UI & Admin](./SUBPHASE-01-11-auth-ui-admin.md)

---

## 1. Summary

Deploy the full stack to Railway: PostgreSQL, API service, static Angular build, and object storage buckets. Swap `ConsoleSmsSender` for a real SMS provider. Run EF migrations on deploy. Verify all Phase 01 acceptance criteria on the production environment.

---

## 2. SPEC references

| SPEC section | Topic |
| ------------ | ----- |
| Section 15.7 | Authentication acceptance criteria (full E2E) |
| Section 21 | Infrastructure and operations |
| Section 5.1 | Real SMS OTP required for production |
| Section 5.8 | Legal pages on deployed domain |
| Section 16 | Railway hosting, object storage |

**Parent exit criteria:** [PHASE-01-platform-foundation.md](./PHASE-01-platform-foundation.md#8-acceptance-criteria)

---

## 3. What you will learn

- Railway project structure: services, environments, variables, networking
- Build and start commands for .NET API and Angular static site on Railway
- Running EF Core migrations on deploy (startup migration or release command)
- Railway Buckets (S3-compatible) wiring for public/private prefixes
- Swapping DI implementations for production (`ISmsSender`)
- Environment variable management without committing secrets

**Files to read after implementing:**

- `railway.toml` or Railway dashboard service settings
- `api/Dockerfile` (if containerized) or `nixpacks` build config
- `docs/deployment.md` — env var reference (create in this sub-phase)

---

## 4. Deliverables

### Railway services

| Service | Type | Detail |
| ------- | ---- | ------ |
| `postgres` | PostgreSQL | Managed Railway Postgres plugin |
| `api` | Web service | ASP.NET Core API; connects to Postgres |
| `web` | Static site | Angular production build output (`web/dist/`) |

### API deploy configuration

| Item | Detail |
| ---- | ------ |
| Build | `dotnet publish -c Release` |
| Start | `dotnet Amanah.Api.dll` |
| Port | Railway `PORT` env var |
| Migrations | Run `dotnet ef database update` on startup or as pre-deploy command |
| Health check | `GET /health` |

### Web deploy configuration

| Item | Detail |
| ---- | ------ |
| Build | `npm ci && npm run build` in `web/` |
| Output | `dist/web/browser` (or Angular 19+ output path) |
| API URL | Production API base URL injected at build time (`environment.prod.ts`) |

### Railway Buckets

| Bucket/prefix | Purpose |
| ------------- | ------- |
| Public prefix | Report photos for non-private categories (Phase 02) |
| Private prefix | Private report photos, claim photos (Phase 02/05) |

Wire credentials as env vars. **No upload endpoints in Phase 01** — configuration only.

### Real SMS provider

| Item | Detail |
| ---- | ------ |
| Interface | Existing `ISmsSender` from sub-phase 06 |
| Implementation | Choose provider (e.g. Twilio, Vonage, local Egyptian provider); implement `SendOtpAsync` |
| Registration | `services.AddSingleton<ISmsSender, RealSmsSender>()` in production |
| Dev | `ConsoleSmsSender` remains for local development |

### Environment variables (document in `docs/deployment.md`)

| Variable | Service | Purpose |
| -------- | ------- | ------- |
| `DATABASE_URL` | API | Postgres connection (Railway plugin reference) |
| `JWT_SECRET` | API | Access token signing key |
| `JWT_REFRESH_SECRET` | API | Refresh token HMAC key (if separate) |
| `ADMIN_PHONE` | API | Admin bootstrap phone |
| `TURNSTILE_SECRET_KEY` | API | CAPTCHA server verification |
| `TURNSTILE_SITE_KEY` | Web (build) | CAPTCHA widget site key |
| `SMS_PROVIDER_*` | API | Provider-specific credentials |
| `BUCKET_*` | API | S3-compatible bucket endpoint, keys, bucket names |
| `ASPNETCORE_ENVIRONMENT` | API | `Production` |

---

## 5. Step-by-step implementation order

1. Create Railway project; add PostgreSQL service
2. Configure API service: repo root or `api/` directory, build/start commands
3. Set API environment variables in Railway dashboard
4. Deploy API; verify `/health` responds
5. Confirm migrations applied (check tables in Railway Postgres console)
6. Configure web static service; set production API URL
7. Deploy web; verify static pages load with RTL
8. Configure Railway Buckets; set bucket env vars on API (no endpoints yet)
9. Implement and register real `ISmsSender`
10. Create `docs/deployment.md` with full env var reference
11. Run full acceptance criteria checklist on deployed environment

---

## 6. Out of scope

- Custom domain (deferred to Phase 08 — Section 14)
- Photo upload endpoints (Phase 02)
- CI/CD pipeline (optional; not required for Phase 01 exit)
- Transactional email (Phase 03)

---

## 7. Validation gate

### Phase 01 acceptance criteria (SPEC 15.7) — on deployed environment

- [ ] **Successful send:** bot check passed + phone under limits → real SMS received on Egyptian mobile
- [ ] **Resend cooldown:** resend within 120s blocked with clear message; no SMS sent
- [ ] **Hourly send limit:** after 2 sends in rolling hour, blocked; no SMS sent
- [ ] **Daily send limit:** after 3 sends in Cairo day, blocked; no SMS sent
- [ ] **Verification attempt limit:** 3 wrong codes void the code; new code required
- [ ] **Account creation point:** abandon after verify but before register → no account; same phone can restart
- [ ] **Banned sign-in:** banned user refused with ban reason
- [ ] **Provider outage:** SMS failure returns temporary-unavailable message; no access granted

### Additional phase gate

- [ ] Admin role guard blocks non-admin from `/admin` on deployed URL
- [ ] JWT access 15 min + refresh 30 days with rotation on refresh
- [ ] Logout everywhere revokes all refresh tokens
- [ ] Seed data: 8 categories with field defs, 27 governorates present

### Definition of done — automated (run against deploy or CI)

- [ ] OTP send limits (cooldown, hourly, daily) with no SMS when blocked
- [ ] OTP verification: success, 3-attempt void, expired code rejection
- [ ] Account created only after display name + ToS; abandoned signup leaves no `User` row
- [ ] JWT issue, refresh rotation, logout, logout-everywhere
- [ ] Banned user rejected at login and token refresh
- [ ] Admin bootstrap: seeded phone has `Admin` role
- [ ] API error contract shape on validation and auth failures

### Manual smoke checklist

- [ ] Migrations apply cleanly on fresh Railway Postgres
- [ ] Real SMS OTP received on Egyptian mobile number
- [ ] Complete signup with Arabic display name; RTL layout correct
- [ ] Terms, Privacy, Safety, Support pages reachable from footer (logged out)
- [ ] Non-admin user blocked from `/admin`
- [ ] Bot check blocks OTP send when failed
- [ ] Browser smoke: app loads in Chrome (latest); mobile viewport RTL layout acceptable (Section 11.1 baseline — full matrix in Phase 08 sub-phase 11)

---

## 8. Exit criteria

- [ ] All acceptance criteria pass on deployed environment
- [ ] `docs/deployment.md` documents all environment variables
- [ ] Mark sub-phase 12 complete in [phase-01/README.md](./README.md)
- [ ] Mark Phase 01 complete in [PHASE-01-platform-foundation.md](./PHASE-01-platform-foundation.md)
