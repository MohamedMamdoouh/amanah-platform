# Sub-phase 02 — Monorepo Scaffold

**Status:** Not started  
**Prerequisites:** [Sub-phase 01 — Decisions & Contracts](./SUBPHASE-01-01-decisions.md)

---

## 1. Summary

Create the runnable monorepo skeleton: ASP.NET Core Web API, Angular PWA, and local PostgreSQL via Docker Compose. Both apps boot and communicate at a basic level. No auth, EF Core, or Railway yet.

---

## 2. SPEC references

| SPEC section | Topic |
| ------------ | ----- |
| Section 16 | Architecture and stack (Angular PWA + ASP.NET Core + PostgreSQL) |
| Section 11.1 | Browser support baseline |

---

## 3. What you will learn

- Monorepo layout conventions for a full-stack project
- ASP.NET Core `Program.cs` minimal hosting model and middleware pipeline order
- Angular standalone components, project structure, and `angular.json` basics
- Docker Compose for local PostgreSQL without polluting the host machine

**Files to read after implementing:**

- `api/Program.cs` — entry point and middleware registration
- `web/src/index.html` — RTL root attributes
- `docker-compose.yml` — local Postgres service definition

---

## 4. Deliverables

### Repository layout

```
Amanah/
├── api/                    # ASP.NET Core Web API
│   ├── Amanah.Api.csproj
│   ├── Program.cs
│   └── appsettings.Development.json
├── web/                    # Angular PWA
│   ├── angular.json
│   ├── src/
│   │   ├── index.html      # dir="rtl" lang="ar"
│   │   └── app/
│   └── ngsw-config.json    # placeholder; configured in sub-phase 10
├── docker-compose.yml      # PostgreSQL 16 for local dev
├── .env.example            # Root env template (no secrets)
└── README.md               # How to run locally
```

### API (`api/`)

| Item | Detail |
| ---- | ------ |
| Framework | ASP.NET Core 8 Web API |
| Endpoint | `GET /health` → `{ "status": "ok" }` |
| CORS | Allow `http://localhost:4200` (Angular dev server) |
| Config | `ConnectionStrings__Default` pointing to Docker Postgres (used in sub-phase 04) |

### Angular (`web/`)

| Item | Detail |
| ---- | ------ |
| Framework | Angular 19+ (standalone components) |
| Root | `<html lang="ar" dir="rtl">` in `index.html` |
| Route | `/` — placeholder home component with Arabic greeting |
| PWA | `@angular/pwa` package added; service worker configured in sub-phase 10 |

### Docker Compose

| Service | Detail |
| ------- | ------ |
| `postgres` | PostgreSQL 16, port `5432`, database `amanah`, user/password in `.env.example` |
| Volume | Named volume for data persistence across restarts |

### Environment templates

`.env.example` at repo root (and `api/.env.example` if needed):

```
POSTGRES_HOST=localhost
POSTGRES_PORT=5432
POSTGRES_DB=amanah
POSTGRES_USER=amanah
POSTGRES_PASSWORD=amanah_dev
```

Document future variables (commented) for JWT, SMS, Turnstile, `ADMIN_PHONE` — wired in later sub-phases.

---

## 5. Step-by-step implementation order

1. Create `api/` with `dotnet new webapi`; remove template weather endpoint; add `/health`
2. Configure CORS for `localhost:4200`
3. Create `web/` with `ng new` (routing yes, SCSS, standalone); set RTL on `index.html`
4. Add placeholder home component at `/`
5. Create `docker-compose.yml` with PostgreSQL 16
6. Write root `README.md` with run instructions
7. Create `.env.example` files
8. Verify all three pieces start independently

---

## 6. Out of scope

- EF Core, migrations, database entities
- Auth endpoints or JWT
- Railway deployment
- PWA service worker activation (sub-phase 10)
- API error contract middleware (sub-phase 03)

---

## 7. Validation gate

### Automated checks

- [ ] `dotnet test` passes (no tests yet, or empty test project compiles)
- [ ] `ng build` succeeds without errors

### Manual smoke checklist

- [ ] `docker compose up -d` starts Postgres; `psql` or connection test succeeds
- [ ] `dotnet run` in `api/` → `curl http://localhost:5000/health` returns `{ "status": "ok" }`
- [ ] `ng serve` in `web/` → `http://localhost:4200` shows RTL placeholder home
- [ ] Browser dev tools show `dir="rtl"` on `<html>` element

---

## 8. Exit criteria

- [ ] All validation gate items pass
- [ ] README documents how to start API, web, and Postgres
- [ ] Mark sub-phase 02 complete in [phase-01/README.md](./README.md)
