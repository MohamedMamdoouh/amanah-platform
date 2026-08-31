# Amanah

Lost-and-found platform for Egypt. [SPEC](docs/SPEC.md) · [API error contract](docs/api-error-contract.md) · [Phase 01](docs/PHASE-01-platform-foundation.md) · [Deployment](docs/deployment.md)

## Local run

Prerequisites: PostgreSQL 16+ on Windows (db `amanah`, user `amanah`, password `amanah_dev`).

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

Single **Render** Docker service (API + Angular) + **Supabase** Postgres + **Cloudflare R2**. See [docs/deployment.md](docs/deployment.md).
