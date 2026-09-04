# Amanah

Lost-and-found platform for Egypt — moderated listings, ownership verification, and in-app messaging. Arabic RTL UI.

**Docs:** [SPEC](specs/SPEC.md) · [API conventions](specs/00-api-conventions.md) · [Phase specs](specs/README.md) · [Deployment](docs/deployment.md)

## Status

| Phase | Topic                                                       | Status       |
| ----- | ----------------------------------------------------------- | ------------ |
| 01    | Platform foundation (auth, sessions, deploy, seeds)         | **Complete** |
| 02    | Report submission                                           | **Complete** |
| 03–08 | Moderation, browse, claims, chat, lifecycle, trust & safety | Planned      |

**Shipped today (Phase 01):** phone OTP sign-up and password sign-in, JWT sessions with refresh rotation, admin role guard, catalog seed data (8 categories, 27 governorates), Arabic RTL SPA with legal/support pages, full database schema, production Docker deploy.

**Shipped (Phase 02):** report submission API (create, mine, detail, withdraw), photo upload pipeline (R2 + in-memory fallback), catalog endpoints, validation/quota/contact-info rules, integration tests, and Angular UI (lost/found forms, My Reports, photo upload, i18n).

**Phase 02 groundwork (Phase 01 carry-over):** report quota service, field validators, contact-info detection, search-text builder, `GET /api/v1/categories` and `GET /api/v1/governorates`.

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

Connection string: `api/appsettings.Development.json`. Production env var names: `.env.example`. SMS uses `ConsoleSmsSender` in Development (OTP printed to the API console).

## Tests

Requires **Docker** (Testcontainers). Integration tests start **PostgreSQL 16** (`postgres:16`) per test factory — same engine and Npgsql provider as local dev and Supabase production.

```bash
dotnet test api.Tests/Amanah.Api.Tests.csproj
```

## Production deploy

Single **Render** Docker service (API + Angular) + **Supabase** Postgres (Session pooler on Render) + **Cloudflare R2** + **Unimtx** SMS. Dashboard setup only — no `render.yaml`. See [docs/deployment.md](docs/deployment.md).
