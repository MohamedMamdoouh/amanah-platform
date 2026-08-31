# Deployment

Phase 01 MVP runs on a **$0/month** stack. SMS is pay-per-message (Unimtx).

| Component              | Provider             |
| ---------------------- | -------------------- |
| Angular PWA + .NET API | Render Free (Docker) |
| PostgreSQL             | Supabase Free        |
| Object storage         | Cloudflare R2        |

One Render Docker service serves the API (`/api/v1/*`), health check (`/health`), and Angular SPA (`/*`). Local dev: `dotnet run` + `ng serve` with `web/proxy.conf.json`.

---

## Setup order

1. **Supabase** — create project; run `CREATE EXTENSION IF NOT EXISTS pg_trgm;`
2. **Render** — Docker web service; set env vars; verify `/health`
3. **Cloudflare R2** — create bucket; set `Bucket__*` env vars
4. **Unimtx** — create account, top up, set `Sms__ApiKey`
5. **Keepalive** — `.github/workflows/keepalive.yml` pings `/health` every 10 min (set `KEEPALIVE_URL` in that file to your Render URL)

---

## Render

Create a **Web Service** in the [Render dashboard](https://dashboard.render.com/) (repo: `MohamedMamdoouh/amanah-platform`):

| Setting           | Value              |
| ----------------- | ------------------ |
| Runtime           | Docker             |
| Dockerfile path   | `api/Dockerfile`   |
| Docker context    | `.` (repo root)    |
| Plan              | `free`             |
| Region            | `frankfurt`        |
| Health check path | `/health`          |

Set `Cors__AllowedOrigins__0` and Cloudflare Turnstile domains to your Render URL.

### Environment variables

| Variable                      | Notes                                      |
| ----------------------------- | ------------------------------------------ |
| `ASPNETCORE_ENVIRONMENT`      | `Production`                               |
| `TURNSTILE_SITE_KEY`          | Build-time — baked into Angular            |
| `ConnectionStrings__Default`  | Supabase **Session pooler** (see below)  |
| `Jwt__AccessTokenSigningKey`  | Random string, min 32 chars                |
| `Jwt__HandoffTokenSigningKey` | Random string, min 32 chars                |
| `Turnstile__SecretKey`        | Cloudflare Turnstile secret                |
| `ADMIN_PHONE`                 | Admin bootstrap phone (`+20...`)           |
| `Cors__AllowedOrigins__0`     | Your Render URL (no trailing slash)        |
| `Bucket__Endpoint`            | R2 S3 endpoint                             |
| `Bucket__AccessKey`           | R2 access key                              |
| `Bucket__SecretKey`           | R2 secret key                              |
| `Bucket__Name`                | R2 bucket name                             |
| `Sms__ApiKey`                 | Unimtx AccessKey ID                        |

---

## Supabase

On Render, use the **Session pooler** connection string (port 5432, `sslmode=require`) — not the direct connection. Render free tier cannot reach Supabase over IPv6.

Set as `ConnectionStrings__Default`. Local dev uses the direct connection in `api/appsettings.Development.json`.

---

## Free-tier notes

| Issue             | Mitigation                                      |
| ----------------- | ----------------------------------------------- |
| Render cold start | GitHub Actions keepalive workflow; upgrade to paid |
| Supabase pause    | Keepalive hits DB via `/health`                    |
| SMS cost          | Monitor Unimtx balance                          |

Before public launch, upgrade Render to a paid plan (~$7/mo) for always-on.

### Keepalive

Set `KEEPALIVE_URL` at the top of `.github/workflows/keepalive.yml` to your Render base URL (no trailing slash). The workflow runs every 10 minutes and fails visibly if `/health` is not 200. Use **Actions → Keepalive → Run workflow** to test after pushing.

---

## Troubleshooting

| Symptom                          | Fix                                                          |
| -------------------------------- | ------------------------------------------------------------ |
| DB connection fails (IPv6 error) | Use Supabase **Session pooler**, not direct connection       |
| Turnstile invalid domain         | Add your `*.onrender.com` hostname in Cloudflare Turnstile   |
| CORS errors                      | `Cors__AllowedOrigins__0` must match Render URL exactly      |

---

## Local development

See [README.md](../README.md). Copy [.env.example](../.env.example) for variable naming.
