# Deployment

Production uses a **$0/month stack** for Phase 01 MVP and pre-launch testing. See tradeoffs below before opening to the public.

| Component | Provider | Monthly cost |
| --------- | -------- | ------------ |
| Angular PWA + .NET API | Render Free (Docker) | $0 |
| PostgreSQL | Supabase Free | $0 |
| Object storage | Cloudflare R2 | $0 |

SMS is pay-per-message (separate from hosting).

---

## Architecture

```
Browser → Render (one Docker web service)
              ├── /api/v1/*  → ASP.NET API
              ├── /health    → health probe
              └── /*           → Angular SPA (wwwroot)
              ↓
    Supabase Postgres + Cloudflare R2
```

One origin serves the Angular app and API. Production uses `apiBaseUrl: '/api/v1'` and refresh-token cookies on `/api/v1/auth` (`SameSite=Lax`) without a separate proxy.

**Local dev:** `dotnet run` + `ng serve` with `web/proxy.conf.json` proxying `/api` to the API.

---

## Deployment order

1. **Supabase** — create project; enable `pg_trgm`; note connection string
2. **Render** — create Docker web service in the dashboard; set env vars; verify `/health` and SPA routes
3. **Cloudflare R2** — create bucket; set `Bucket__*` on Render
4. **eSMS Africa SMS** — create account, register sender ID, top up wallet, set `Sms__*` on Render
5. **Keepalive cron** — optional during active testing (see below)
6. Run Phase 01 acceptance checklist on the Render URL

---

## Supabase (PostgreSQL)

1. Create a free project (EU region if available).
2. In the SQL editor, run:

```sql
CREATE EXTENSION IF NOT EXISTS pg_trgm;
```

3. Use the **direct** connection string (port **5432**, `sslmode=require`) for EF Core on Render — not the transaction pooler.

Example shape:

```
Host=db.<ref>.supabase.co;Port=5432;Database=postgres;Username=postgres;Password=<password>;SSL Mode=Require
```

Set on Render as `ConnectionStrings__Default`.

**Do not** use Render free Postgres for production data (90-day lifetime).

**Free-tier limits:** 500 MB storage; project pauses after ~1 week of inactivity.

---

## Render (API + frontend)

Create a **Web Service** in the [Render dashboard](https://dashboard.render.com/) (**New → Web Service** → connect `MohamedMamdoouh/amanah-platform`):

| Setting | Value |
| ------- | ----- |
| Service name | `amanah` → `https://amanah.onrender.com` |
| Runtime | Docker |
| Dockerfile path | `api/Dockerfile` |
| Docker context | repository root (`.`) |
| Plan | `free` |
| Region | `frankfurt` (EU) |
| Health check path | `/health` |

A payment method is required on the Render account before creating services (free plan still applies).

The Docker image builds Angular (`web/`) and copies output to `wwwroot/`, then publishes the .NET API. Migrations run on startup (`Database:AutoMigrate: true`).

Verify after deploy:

```bash
curl https://amanah.onrender.com/health
curl https://amanah.onrender.com/
curl https://amanah.onrender.com/login
```

### Render environment variables

| Variable | Required | Purpose |
| -------- | -------- | ------- |
| `ASPNETCORE_ENVIRONMENT` | Yes | `Production` |
| `TURNSTILE_SITE_KEY` | Yes | Docker build — public Turnstile site key (baked into Angular build) |
| `ConnectionStrings__Default` | Yes | Supabase Postgres connection string |
| `Jwt__AccessTokenSigningKey` | Yes | Access JWT signing key (min 32 chars) |
| `Jwt__HandoffTokenSigningKey` | Yes | OTP handoff token signing key (min 32 chars) |
| `Turnstile__SecretKey` | Yes | Cloudflare Turnstile server secret |
| `ADMIN_PHONE` | Yes | Admin bootstrap phone (`+20...`) |
| `Cors__AllowedOrigins__0` | Yes | Render service URL (e.g. `https://amanah.onrender.com`) |
| `Bucket__Endpoint` | Phase 01 config | R2 S3 endpoint |
| `Bucket__AccessKey` | Phase 01 config | R2 access key |
| `Bucket__SecretKey` | Phase 01 config | R2 secret key |
| `Bucket__Name` | Phase 01 config | R2 bucket name |
| `Sms__ApiKey` | Yes | eSMS Africa API key (`esms_live_...` or `esms_test_...`) |
| `Sms__SenderId` | Yes | Approved alphanumeric sender ID for Egypt (e.g. `Amanah`) |

Add `Cors__AllowedOrigins__1` for additional origins (custom domain later).

Generate `Jwt__AccessTokenSigningKey` and `Jwt__HandoffTokenSigningKey` as random strings (min 32 characters each) and set them in the Render dashboard before the first deploy.

---

## Cloudflare R2 (object storage)

Phase 01: configuration only — no upload endpoints.

1. Create bucket (e.g. `amanah-media`).
2. Create API token with Object Read & Write.
3. Set on Render API:

| Variable | Example |
| -------- | ------- |
| `Bucket__Endpoint` | `https://<account-id>.r2.cloudflarestorage.com` |
| `Bucket__AccessKey` | R2 token access key |
| `Bucket__SecretKey` | R2 token secret |
| `Bucket__Name` | `amanah-media` |

Use prefixes `public/` and `private/` when upload endpoints ship in Phase 02.

---

## eSMS Africa (SMS / OTP)

Production uses [eSMS Africa](https://esmsafrica.io/sms/egypt) via `EsmsAfricaSmsSender` (`ISmsSender`). Pay-as-you-go wallet top-up; Egypt OTP is ~EGP 0.62–0.73 per SMS (~$0.012–0.015).

1. Create an account at [auth.esmsafrica.io](https://auth.esmsafrica.io).
2. Create an API key (start with `test` for staging; use `live` for production).
3. Register an alphanumeric sender ID for Egypt (e.g. `Amanah`); NTRA approval may take a few days.
4. Top up your wallet (minimum EGP 50).
5. Set on Render:

| Variable | Source |
| -------- | ------ |
| `Sms__ApiKey` | eSMS Africa dashboard → API Keys |
| `Sms__SenderId` | Your approved sender ID |

OTP message body (Arabic): `رمز التحقق من أمانة: {code}`.

Local development continues to log OTP codes to the console (`ConsoleSmsSender`).

**Low balance:** API returns HTTP 422 when wallet balance is too low; top up and retry failed outbox rows if needed.

---

## Keepalive cron (free tier mitigation)

During active development and Phase 01 gate testing, use [cron-job.org](https://cron-job.org) (or similar):

| Target | Interval | Purpose |
| ------ | -------- | ------- |
| `https://amanah.onrender.com/health` | Every 10–14 minutes | Mitigate Render cold sleep + Supabase pause (DB-touching health) |

**Not production-grade.** Cold starts can still occur between cron ticks. Budget ~$5/month for always-on API before public launch.

---

## Free-tier tradeoffs

| Issue | Impact | Mitigation |
| ----- | ------ | ---------- |
| Render cold start | 30–60s delay after ~15 min idle; OTP may feel broken | Keepalive cron during testing; upgrade API tier |
| Supabase pause | DB unavailable after ~1 week idle | Keepalive with DB-touching `/health` |
| SMS cost | Independent of hosting; grows with OTP traffic | Monitor eSMS Africa wallet balance |

---

## Upgrade path (~$5/month)

Before opening to real users:

| Component | Upgrade to | Est. cost |
| --------- | ---------- | --------- |
| API + frontend | Render paid plan (always-on) | ~$7/mo |
| PostgreSQL | Keep Supabase free with keepalive, or Supabase Pro | $0–25/mo |
| Object storage | Keep Cloudflare R2 | $0 |

---

## OTP / SMS operational risks

When wiring a real `ISmsSender`:

| Risk | Mitigation |
| ---- | ---------- |
| SMS accepted but HTTP response lost | Outbox stays `Pending`; worker retries with outbox Id as idempotency key. Client may see 503; user can verify if SMS arrived. |
| Client timeout after SMS sent | Dispatch uses `CancellationToken.None` after DB commit. |
| Stale outbox row after crash | Atomic Pending → Dispatching claim; stale Dispatching reclaimed. |
| `Sent` update fails after delivery | Rare; limits may under-count by one. Monitor logs. |

---

## Local development

See [README.md](../README.md). API on `localhost:5000`; Angular on `localhost:4200` with `proxy.conf.json` proxying `/api` to the API.

Copy [.env.example](../.env.example) for variable naming reference. Local connection string is in `api/appsettings.Development.json`.
