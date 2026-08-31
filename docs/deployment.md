# Deployment

Production uses a **$0/month stack** for Phase 01 MVP and pre-launch testing. See tradeoffs below before opening to the public.

| Component              | Provider             | Monthly cost |
| ---------------------- | -------------------- | ------------ |
| Angular PWA + .NET API | Render Free (Docker) | $0           |
| PostgreSQL             | Supabase Free        | $0           |
| Object storage         | Cloudflare R2        | $0           |

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
4. **Unimtx SMS** — create account, copy AccessKey ID, top up balance, set `Sms__ApiKey` on Render
5. **Keepalive cron** — optional during active testing (see below)
6. Run Phase 01 acceptance checklist on the Render URL

---

## Supabase (PostgreSQL)

1. Create a free project (EU region if available).
2. In the SQL editor, run:

```sql
CREATE EXTENSION IF NOT EXISTS pg_trgm;
```

3. Use the **Session pooler** connection string (port **5432**, `sslmode=require`) on Render — **not** the direct connection. Render free tier cannot reach Supabase over IPv6; the pooler uses IPv4. Username is `postgres.<project-ref>` (e.g. `postgres.akdvohdqkxewpdozouru`).

   **Local dev** uses direct connection (`db.<ref>.supabase.co`, username `postgres`) in `api/appsettings.Development.json`.

Example shape (Render / production):

```
Host=aws-0-eu-west-1.pooler.supabase.com;Port=5432;Database=postgres;Username=postgres.<ref>;Password=<password>;SSL Mode=Require
```

Set on Render as `ConnectionStrings__Default`.

**Do not** use Render free Postgres for production data (90-day lifetime).

**Free-tier limits:** 500 MB storage; project pauses after ~1 week of inactivity.

---

## Render (API + frontend)

Create a **Web Service** in the [Render dashboard](https://dashboard.render.com/) (**New → Web Service** → connect `MohamedMamdoouh/amanah-platform`):

| Setting           | Value                                                                    |
| ----------------- | ------------------------------------------------------------------------ |
| Service name      | `amanah` (Render assigns URL, e.g. `https://amanah-egh5.onrender.com`) |
| Runtime           | Docker                                                                   |
| Dockerfile path   | `api/Dockerfile`                                                         |
| Docker context    | repository root (`.`)                                                    |
| Plan              | `free`                                                                   |
| Region            | `frankfurt` (EU)                                                         |
| Health check path | `/health`                                                                |

A payment method is required on the Render account before creating services (free plan still applies).

The Docker image builds Angular (`web/`) via `web/scripts/generate-production-env.mjs` (injects `TURNSTILE_SITE_KEY` into the production build), copies output to `wwwroot`, then publishes the .NET API. The runtime image sets `DOTNET_HOSTBUILDER__RELOADCONFIGONCHANGE=false` to avoid inotify limits on Render. Migrations run on startup (`Database:AutoMigrate: true`).

Use your **actual Render URL** (top of the service page) for `Cors__AllowedOrigins__0` and Cloudflare Turnstile widget domains.

Verify after deploy (replace with your URL):

```bash
curl https://amanah-egh5.onrender.com/health
curl https://amanah-egh5.onrender.com/
curl https://amanah-egh5.onrender.com/login
```

### Render environment variables

| Variable                      | Required        | Purpose                                                             |
| ----------------------------- | --------------- | ------------------------------------------------------------------- |
| `ASPNETCORE_ENVIRONMENT`      | Yes             | `Production`                                                        |
| `TURNSTILE_SITE_KEY`          | Yes             | Docker build — public Turnstile site key (baked into Angular build) |
| `ConnectionStrings__Default`  | Yes             | Supabase Postgres connection string                                 |
| `Jwt__AccessTokenSigningKey`  | Yes             | Access JWT signing key (min 32 chars)                               |
| `Jwt__HandoffTokenSigningKey` | Yes             | OTP handoff token signing key (min 32 chars)                        |
| `Turnstile__SecretKey`        | Yes             | Cloudflare Turnstile server secret                                  |
| `ADMIN_PHONE`                 | Yes             | Admin bootstrap phone (`+20...`)                                    |
| `Cors__AllowedOrigins__0`     | Yes             | Your Render service URL (e.g. `https://amanah-egh5.onrender.com`)   |
| `Bucket__Endpoint`            | Phase 01 config | R2 S3 endpoint                                                      |
| `Bucket__AccessKey`           | Phase 01 config | R2 access key                                                       |
| `Bucket__SecretKey`           | Phase 01 config | R2 secret key                                                       |
| `Bucket__Name`                | Phase 01 config | R2 bucket name                                                      |
| `Sms__ApiKey`                 | Yes             | Unimtx AccessKey ID (Console → Credentials)                           |

Add `Cors__AllowedOrigins__1` for additional origins (custom domain later).

Generate `Jwt__AccessTokenSigningKey` and `Jwt__HandoffTokenSigningKey` as random strings (min 32 characters each) and set them in the Render dashboard before the first deploy.

---

## Cloudflare R2 (object storage)

Phase 01: configuration only — no upload endpoints.

1. Create bucket (e.g. `amanah-media`).
2. Create API token with Object Read & Write.
3. Set on Render API:

| Variable            | Example                                         |
| ------------------- | ----------------------------------------------- |
| `Bucket__Endpoint`  | `https://<account-id>.r2.cloudflarestorage.com` |
| `Bucket__AccessKey` | R2 token access key                             |
| `Bucket__SecretKey` | R2 token secret                                 |
| `Bucket__Name`      | `amanah-media`                                  |

Use prefixes `public/` and `private/` when upload endpoints ship in Phase 02.

---

## Unimtx (SMS / OTP)

Production uses [Unimtx](https://www.unimtx.com/sms/eg) via `UnimtxSmsSender` (`ISmsSender`), calling the [OTP Send API](https://www.unimtx.com/docs/api/send-otp) (`otp.send`). Pay-as-you-go balance top-up; Egypt OTP is ~$0.135 per SMS.

1. Create an account at [unimtx.com](https://www.unimtx.com).
2. Copy your **AccessKey ID** from Console → Credentials.
3. Top up your account balance.
4. Set on Render:

| Variable      | Source                              |
| ------------- | ----------------------------------- |
| `Sms__ApiKey` | Unimtx Console → Credentials → AccessKey ID |

OTP delivery uses Unimtx's built-in OTP template (not a custom Arabic message). Amanah still generates and verifies codes locally.

Local development continues to log OTP codes to the console (`ConsoleSmsSender`).

**Low balance:** API returns error code `105400` (`InsufficientFunds`); top up and retry failed outbox rows if needed.

**IP restriction:** If enabled in the Unimtx console, allowlist your Render service egress IP.

---

## Keepalive (free tier mitigation)

`.github/workflows/keepalive.yml` pings `/health` every 10 minutes on `https://amanah-egh5.onrender.com`. Update `KEEPALIVE_URL` in that file if your Render URL changes.

After pushing, use **Actions → Keepalive → Run workflow** to test. The workflow fails visibly if `/health` is not HTTP 200.

**Not production-grade.** Cold starts can still occur between ticks. Budget ~$5/month for always-on API before public launch.

---

## Free-tier tradeoffs

| Issue             | Impact                                               | Mitigation                                      |
| ----------------- | ---------------------------------------------------- | ----------------------------------------------- |
| Render cold start | 30–60s delay after ~15 min idle; OTP may feel broken | Keepalive cron during testing; upgrade API tier |
| Supabase pause    | DB unavailable after ~1 week idle                    | Keepalive with DB-touching `/health`            |
| SMS cost          | Independent of hosting; grows with OTP traffic       | Monitor Unimtx account balance                  |

---

## Upgrade path (~$5/month)

Before opening to real users:

| Component      | Upgrade to                                         | Est. cost |
| -------------- | -------------------------------------------------- | --------- |
| API + frontend | Render paid plan (always-on)                       | ~$7/mo    |
| PostgreSQL     | Keep Supabase free with keepalive, or Supabase Pro | $0–25/mo  |
| Object storage | Keep Cloudflare R2                                 | $0        |

---

## OTP / SMS operational risks

When wiring a real `ISmsSender`:

| Risk                                | Mitigation                                                                                                                    |
| ----------------------------------- | ----------------------------------------------------------------------------------------------------------------------------- |
| SMS accepted but HTTP response lost | Outbox stays `Pending`; worker retries with outbox Id as idempotency key. Client may see 503; user can verify if SMS arrived. |
| Client timeout after SMS sent       | Dispatch uses `CancellationToken.None` after DB commit.                                                                       |
| Stale outbox row after crash        | Atomic Pending → Dispatching claim; stale Dispatching reclaimed.                                                              |
| `Sent` update fails after delivery  | Rare; limits may under-count by one. Monitor logs.                                                                            |

---

## Troubleshooting

| Symptom | Likely cause | Fix |
| ------- | ------------ | --- |
| `Failed to connect to [2a05:...]:5432` / `Network is unreachable` | Supabase **direct** connection uses IPv6; Render cannot reach it | Use **Session pooler** connection string on Render |
| `inotify instances has been reached` at startup | Config file watching in Docker | Set in `api/Dockerfile`: `ENV DOTNET_HOSTBUILDER__RELOADCONFIGONCHANGE=false` |
| Docker build fails on `node -e` / Angular env | Inline shell escaping | Uses `web/scripts/generate-production-env.mjs` — ensure latest `api/Dockerfile` is deployed |
| Turnstile invalid domain | Widget domains don't match Render URL | Add your `*.onrender.com` hostname in Cloudflare Turnstile |
| CORS errors in browser | `Cors__AllowedOrigins__0` mismatch | Must exactly match your Render URL (scheme + host, no trailing slash) |

---

## Local development

See [README.md](../README.md). API on `localhost:5000`; Angular on `localhost:4200` with `proxy.conf.json` proxying `/api` to the API.

Copy [.env.example](../.env.example) for variable naming reference. Local connection string is in `api/appsettings.Development.json`.
