# Amanah

Lost-and-found platform for Egypt. [SPEC](docs/SPEC.md) · [Phases](docs/README.md) · [Phase 01](docs/PHASE-01-platform-foundation.md)

## Local run

Prerequisites: PostgreSQL 16+ on Windows (db `amanah`, user `amanah`, password `amanah_dev`).

```bash
cd api && dotnet run
cd web && npm start           # http://localhost:4200
```

Copy `.env.example` for reference. Connection string in `api/appsettings.Development.json`.

## Tests

Requires **Docker** (Testcontainers). Integration tests start **PostgreSQL 16** (`postgres:16`) per `IClassFixture` factory — same database engine and Npgsql provider as local dev and Railway production.

```bash
dotnet test api.Tests/Amanah.Api.Tests.csproj
```
