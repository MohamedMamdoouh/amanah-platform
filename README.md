# Amanah

Lost-and-found platform for Egypt. [SPEC](specs/SPEC.md) · [API conventions](specs/00-api-conventions.md) · [Phase 01](specs/01-platform-foundation.md) · [Deployment](docs/deployment.md)

## Local run

Prerequisites: PostgreSQL 16+ on Windows (db `amanah`, user `amanah`, password `amanah_dev`).

Stack: **.NET 10** API, **Angular 19** SPA, **PostgreSQL 16**.

```bash
cd api && dotnet run          # http://localhost:5000
cd web && npm start           # http://localhost:4200 — proxies /api to the API
```

Connection string in `api/appsettings.Development.json`. See `.env.example` for production env var names (Render dashboard).

## Tests

Requires **Docker** (Testcontainers). Integration tests start **PostgreSQL 16** (`postgres:16`) per `IClassFixture` factory — same database engine and Npgsql provider as local dev and Supabase production.

```bash
dotnet test api.Tests/Amanah.Api.Tests.csproj
```

## Production deploy

Single **Render** Docker service (API + Angular) + **Supabase** Postgres (Session pooler on Render) + **Cloudflare R2**. Dashboard setup only — no `render.yaml`. See [docs/deployment.md](docs/deployment.md).
