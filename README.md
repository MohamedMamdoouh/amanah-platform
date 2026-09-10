# Amanah

Lost-and-found platform for Egypt — moderated listings, ownership verification, and in-app messaging. Arabic RTL UI.

**Docs:** [SPEC](specs/SPEC.md) · [API conventions](specs/00-api-conventions.md) · [Phase specs](specs/README.md) · [UI context](docs/ui-ux-context.md) · [Deployment](docs/deployment.md) · [Observability](docs/observability.md)

## Status

| Phase | Topic                                                       | Status       |
| ----- | ----------------------------------------------------------- | ------------ |
| —     | Platform foundation (auth, sessions, deploy, seeds)         | **Complete** |
| 01    | Report submission                                           | **Complete** |
| 02    | Admin moderation (queue, resubmit, categories, email)       | **Complete**¹ |
| 03    | Browse & discovery (search, filters, public detail)         | **Complete** |
| 04–07 | Claims, chat, lifecycle, trust & safety                     | Not started  |

**Next up:** Phase 04 — claims and ownership verification.

¹ Automated tests complete; manual smoke (especially Resend in staging) pending — see [specs/02-admin-moderation.md](specs/02-admin-moderation.md) §9.

### Shipped (platform foundation)

Auth (phone OTP signup, password sign-in, JWT + httpOnly refresh cookie rotation, password reset, logout-everywhere), admin bootstrap, catalog seeds (8 categories, 27 governorates), Arabic RTL SPA with legal/support pages, full DB schema, structured logging + health probes, production Docker deploy on Render.

**Routes:** `/`, `/browse`, `/login`, `/terms`, `/privacy`, `/safety`, `/support`, `/admin` (redirects to moderation)

### Shipped (Phase 01)

Lost/found report submission with category fields, hidden verification detail, contact-info blocking, quotas, photo pipeline (EXIF strip, WebP, R2), and normalized search column for Phase 03 browse.

**Routes:** `/report/lost`, `/report/found`, `/my/reports`, `/my/reports/{id}`

Details: [specs/01-report-submission.md](specs/01-report-submission.md)

### Shipped (Phase 02)

Admin FIFO moderation queue (approve/reject, keyword search), reporter edit/resubmit for rejected reports, category/field CRUD, in-app notification center (`ReportApproved` / `ReportRejected`), admin alert email outbox (Resend).

**Routes:** `/admin/moderation`, `/admin/moderation/{id}`, `/admin/categories`, `/notifications` (+ My Reports Rejected/Published tabs)

Details: [specs/02-admin-moderation.md](specs/02-admin-moderation.md)

### Shipped (Phase 03)

Public browse listing with Arabic keyword search, filters, pagination, and status-aware public detail pages (`/lost/{id}`, `/found/{id}`) with not-found and permanently-unavailable routing. Claim and message action stubs prompt login (full flows in Phases 04–05).

**Routes:** `/browse`, `/lost/{id}`, `/found/{id}`, `/not-found`, `/unavailable`

Details: [specs/03-browse-discovery.md](specs/03-browse-discovery.md)

### Not built yet

Claims, chat, resolution, lifecycle jobs, abuse enforcement — see [phase specs](specs/README.md).

## Stack

| Layer    | Technology                                            |
| -------- | ----------------------------------------------------- |
| API      | .NET 10, ASP.NET Core, EF Core, Npgsql                |
| Web      | Angular 19, ngx-translate (Arabic)                    |
| Database | PostgreSQL 16 (local / Supabase)                      |
| Auth     | Phone OTP (Unimtx SMS), JWT + httpOnly refresh cookie |
| Media    | Cloudflare R2 (`Bucket__*` env vars; fake in-memory storage when unset) |
| Hosting  | Render (single Docker service: API + SPA)             |

## Repository layout

```
api/           ASP.NET Core API
web/           Angular SPA
contracts/     Shared request/response DTOs
api.Tests/     Integration and unit tests (Testcontainers)
specs/         Product spec and phased implementation plans
docs/          Deployment, observability, and UI context
```

## Local run

**Prerequisites:** [.NET 10 SDK](https://dotnet.microsoft.com/download), [Node.js 22+](https://nodejs.org/), PostgreSQL 16+ on Windows (db `amanah`, user `amanah`, password `amanah_dev`).

```bash
# API — http://localhost:5000
cd api && dotnet run

# SPA — http://localhost:4200 (proxies /api to the API)
cd web && npm install && npm start
```

Connection string: `api/appsettings.Development.json`. Production env var names: `.env.example`. SMS uses `ConsoleSmsSender` in Development (OTP printed to the API console). Object storage falls back to in-memory when `Bucket__Endpoint` is unset.

On first startup, migrations and catalog seed run automatically (8 categories, 27 governorates). Dev accounts are bootstrapped from `appsettings.Development.json`:

| Account | Phone (login) | Password | Unlocks |
| ------- | ------------- | -------- | ------- |
| Admin | `01011111111` | `AdminPass123` | `/admin/moderation`, `/admin/categories` |
| User | `01022222222` | `UserPass123` | `/report/lost`, `/report/found`, `/my/reports` |

## Tests

Requires **Docker** (Testcontainers). Integration tests start **PostgreSQL 16** (`postgres:16`) per test factory — same engine and Npgsql provider as local dev and Supabase production.

```bash
dotnet test api.Tests/Amanah.Api.Tests.csproj
```

## Production deploy

Single **Render** Docker service (API + Angular) + **Supabase** Postgres (Session pooler on Render) + **Cloudflare R2** + **Unimtx** SMS. Dashboard setup only — no `render.yaml`. See [docs/deployment.md](docs/deployment.md).

After deploy, verify `/health`, `/health/ready`, sign-in/sign-up, and report submission. See [docs/observability.md](docs/observability.md) for logs, metrics, and alerting.
