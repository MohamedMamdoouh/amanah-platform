# Amanah

Lost-and-found platform for Egypt — moderated listings, ownership verification, and in-app messaging. Arabic RTL UI.

**Docs:** [SPEC](specs/SPEC.md) · [API conventions](specs/00-api-conventions.md) · [Phase specs](specs/README.md) · [Deployment](docs/deployment.md) · [Observability](docs/observability.md)

## Status

| Phase | Topic                                                       | Status       |
| ----- | ----------------------------------------------------------- | ------------ |
| 01    | Platform foundation (auth, sessions, deploy, seeds)         | **Complete** |
| 02    | Report submission                                           | **Complete** |
| 03–08 | Moderation, browse, claims, chat, lifecycle, trust & safety | Planned      |

**Next up:** Phase 03 — admin moderation queue (approve/reject, resubmit rejected reports).

### Shipped (Phase 01)

- Phone OTP sign-up and **password** sign-in (JWT access token + httpOnly refresh cookie rotation)
- Password reset via OTP; logout and logout-everywhere
- Admin role guard and bootstrap admin account
- Catalog seed data (8 categories, 27 governorates)
- Arabic RTL SPA with legal and support pages
- Full database schema (EF Core migrations)
- Structured JSON logging, correlation IDs, `/health` + `/health/ready`, log-emitted metrics
- Production Docker deploy on Render (single service: API + SPA)

### Shipped (Phase 02)

**API**

- `POST /api/v1/reports` — create lost/found report (multipart: JSON + optional photos)
- `GET /api/v1/reports/mine` — reporter's submissions
- `GET /api/v1/reports/{id}` — report detail (reporter or admin only while pending)
- `POST /api/v1/reports/{id}/withdraw` — withdraw while `Pending Review`
- `GET /api/v1/uploads/report-photo/{id}/url` — signed photo URL (reporter/admin)
- `GET /api/v1/categories` and `GET /api/v1/governorates` — cached catalog keys for forms

**Behavior**

- Category-specific fields, hidden verification detail, contact-info blocking, submission quota (3/day), open-report cap (5)
- Photo pipeline: EXIF strip, WebP thumbnails, R2 storage (`public/` / `private/` by category) with in-memory fallback when `Bucket__*` is unset
- Normalized search column populated on write (ready for Phase 04 browse)

**Web**

- `/report/lost`, `/report/found` — submission forms with photo upload
- `/my/reports`, `/my/reports/{id}` — list and detail (withdraw while pending)
- Home CTAs for report submission; browse/search placeholder (Phase 04)
- Admin shell route (`/admin`) — placeholder until Phase 03

**Tests**

- Integration tests for auth, catalog, report submission/access/withdraw, and photo upload
- Unit tests for validators, quota, normalizers, and image processing

### Not built yet

Public browse/search, admin moderation, claims, chat, resolution, lifecycle jobs, and in-app notifications — see [phase specs](specs/README.md).

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
docs/          Deployment and operations
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

## Tests

Requires **Docker** (Testcontainers). Integration tests start **PostgreSQL 16** (`postgres:16`) per test factory — same engine and Npgsql provider as local dev and Supabase production.

```bash
dotnet test api.Tests/Amanah.Api.Tests.csproj
```

## Production deploy

Single **Render** Docker service (API + Angular) + **Supabase** Postgres (Session pooler on Render) + **Cloudflare R2** + **Unimtx** SMS. Dashboard setup only — no `render.yaml`. See [docs/deployment.md](docs/deployment.md).

After deploy, verify `/health`, `/health/ready`, sign-in/sign-up, and report submission. See [docs/observability.md](docs/observability.md) for logs, metrics, and alerting.
