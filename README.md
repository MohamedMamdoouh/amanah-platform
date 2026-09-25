# Amanah

**Amanah** is a lost-and-found service for Egypt. Anyone can browse and search published listings. Sign in to post a report, handle ownership claims, and chat with the other party. Listings are reviewed before they go public, and contact details stay off public posts. The app is Arabic (RTL); this repository is the .NET API and Angular SPA.

**Live site:** [https://amanah-egh5.onrender.com](https://amanah-egh5.onrender.com)

**Further reading:** [Product spec](specs/SPEC.md) · [API conventions](specs/00-api-conventions.md) · [Deployment](docs/deployment.md) · [Observability](docs/observability.md)

---

## Table of Contents

- [Amanah](#amanah)
  - [Table of Contents](#table-of-contents)
  - [Features](#features)
  - [Tech Stack](#tech-stack)
  - [Architecture](#architecture)
    - [Key patterns](#key-patterns)
  - [Getting Started](#getting-started)
    - [Prerequisites](#prerequisites)
    - [Clone](#clone)
    - [Environment setup](#environment-setup)
  - [Running the Project](#running-the-project)
    - [Option 1 — Local API + Angular dev server (recommended for development)](#option-1--local-api--angular-dev-server-recommended-for-development)
    - [Option 2 — Production Docker image](#option-2--production-docker-image)
  - [Configuration](#configuration)
    - [`appsettings.json` structure](#appsettingsjson-structure)
    - [Environment variable overrides](#environment-variable-overrides)
    - [Turnstile site key (frontend)](#turnstile-site-key-frontend)
  - [API Documentation](#api-documentation)
    - [Authentication](#authentication)
    - [Key endpoints](#key-endpoints)
    - [Realtime chat (SignalR)](#realtime-chat-signalr)
  - [Testing](#testing)
  - [Automation](#automation)
  - [Project Structure](#project-structure)
  - [API Response Contract](#api-response-contract)
    - [Success responses](#success-responses)
    - [Error responses](#error-responses)
    - [Front-end integration guide](#front-end-integration-guide)
  - [Design Decisions](#design-decisions)
  - [Deployment](#deployment)

---

## Features

- **Authentication** — Phone OTP signup, password sign-in, JWT access tokens with httpOnly refresh cookie rotation, password reset, logout everywhere
- **Reports** — Lost and found submissions with category-specific fields, optional photos (EXIF strip, WebP), contact-info blocking, and quotas
- **Moderation** — Admin FIFO queue (approve/reject), reporter resubmit for rejected reports, category and field CRUD, admin alert email (Brevo)
- **Browse** — Public listing with search, filters, pagination, and status-aware detail pages (`/lost/{id}`, `/found/{id}`)
- **Claims** — Submit and review ownership claims, presigned private claim photos, competing-claim handling, My Claims
- **Chat & resolution** — SignalR live messaging with REST fallback, attachments, safety banner, confirm resolved / cancel claim
- **Notifications** — In-app notification center with deep links
- **Account** — Deactivation with blockers and reactivation
- **Trust & safety** — Listing flags, admin abuse queue and investigation, user ban/unban, enforcement side effects
- **Lifecycle** — Background jobs for listing expiry, claim timeouts, retention, OTP/session cleanup, and orphaned storage sweeps
- **Rate limiting** — OTP send, login, photo upload, and chat message policies (see `RateLimit` in [api/appsettings.json](api/appsettings.json))
- **Observability** — Structured JSON logs in Production, correlation IDs, log-emitted metrics, split health endpoints ([docs/observability.md](docs/observability.md))

Behavioral detail: [specs/README.md](specs/README.md).

---

## Tech Stack

| Layer            | Technology                                                                 |
| ---------------- | -------------------------------------------------------------------------- |
| Runtime          | .NET 10                                                                    |
| Web framework    | ASP.NET Core 10                                                            |
| Frontend         | Angular 19, ngx-translate (Arabic), SCSS                                   |
| ORM              | Entity Framework Core 10 (PostgreSQL via Npgsql)                           |
| Validation       | FluentValidation                                                           |
| Authentication   | JWT Bearer + httpOnly refresh cookie (`amanah_refresh`)                    |
| Real-time        | ASP.NET Core SignalR (`/hubs/chat`)                                        |
| Media storage    | Cloudflare R2 (S3-compatible via AWSSDK.S3); in-memory fake when unset     |
| SMS              | Unimtx (production); console sender in Development                         |
| Email            | Brevo (admin moderation alerts; no-op when unset)                          |
| Captcha          | Cloudflare Turnstile                                                       |
| Caching          | `Microsoft.Extensions.Caching.Hybrid` (catalog/governorate TTLs)           |
| Containerisation | Multi-stage Docker ([api/Dockerfile](api/Dockerfile))                      |
| Testing          | xUnit, Testcontainers (PostgreSQL 16), `WebApplicationFactory`             |
| API docs         | Swashbuckle OpenAPI + Swagger UI at `/swagger` (all environments)          |
| Production host  | Render (single service: API + built SPA) · Supabase Postgres               |

Central NuGet versions: [Directory.Packages.props](Directory.Packages.props). Frontend: [web/package.json](web/package.json).

---

## Architecture

The repository is a **monolith API** plus an **Angular SPA**, with shared DTOs in a small contracts project:

```text
web/           Angular SPA (RTL, proxies /api and /hubs in development)
api/           ASP.NET Core — controllers, services, EF Core, hosted jobs
contracts/     Request/response types at the API boundary
api.Tests/     Integration and unit tests
```

In **Production**, one process serves the API and static files from `wwwroot` ([api/Extensions/DependencyInjection.cs](api/Extensions/DependencyInjection.cs)). There is no `.sln` file; build projects directly (for example `api/Amanah.Api.csproj`).

### Key patterns

- **Feature services** — Business rules live in scoped services (`ReportService`, `ClaimService`, `ModerationService`, etc.) registered without mirror interfaces unless there is a real second implementation or external boundary.
- **Contracts project** — Public API shapes live in [contracts/](contracts/), keeping controllers thin and decoupled from EF entities.
- **Transactional outboxes** — OTP SMS and admin alert email are written to outbox tables in the same database transaction as the triggering action, then dispatched by background processors.
- **Lifecycle job registry** — `ILifecycleJob` implementations are collected and run on a schedule by `LifecycleJobsHostedService` inside the API process.
- **Storage boundary** — `IBucketStorage` switches between Cloudflare R2 and in-memory fake storage based on configuration.
- **External boundaries** — `ISmsSender`, `ICaptchaVerifier`, and email senders are interface-backed for production providers vs Development fakes.
- **URL versioning** — All HTTP APIs use `/api/v1/...` via `Asp.Versioning.Mvc`.

---

## Getting Started

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- [Node.js 22+](https://nodejs.org/) (matches the Docker web build stage)
- PostgreSQL 16+ for local API runs
- [Docker Desktop](https://www.docker.com/products/docker-desktop/) (required for the test suite)

Default local database (from [api/appsettings.Development.json](api/appsettings.Development.json)):

| Setting  | Value      |
| -------- | ---------- |
| Host     | `localhost` |
| Port     | `5432`     |
| Database | `amanah`   |
| Username | `amanah`   |
| Password | `amanah_dev` |

### Clone

```bash
git clone https://github.com/MohamedMamdoouh/amanah-platform.git
cd amanah-platform
```

### Environment setup

**Local development** — JWT keys, connection string, and bootstrap user keys are already in [api/appsettings.Development.json](api/appsettings.Development.json). Bootstrap accounts are created from `ADMIN_PHONE`, `ADMIN_PASSWORD`, `USER1_PHONE`, `USER2_PHONE`, and `USER_PASSWORD`.

**Production / staging** — Set environment variables on the host (see [Configuration](#configuration)). Names and placeholders: [.env.example](.env.example). Full checklist: [docs/deployment.md](docs/deployment.md).

On startup, when `Database:AutoMigrate` is true (default), the API applies EF Core migrations under [api/Data/Migrations](api/Data/Migrations) and seeds catalog data (7 categories, 27 governorates).

---

## Running the Project

### Option 1 — Local API + Angular dev server (recommended for development)

Run in two terminals:

```bash
# API — http://localhost:5000
cd api && dotnet run

# SPA — http://localhost:4200 (proxies /api and /hubs to the API)
cd web && npm install && npm start
```

| Service | URL |
| ------- | --- |
| API     | <http://localhost:5000> |
| SPA     | <http://localhost:4200> |

The dev server uses [web/proxy.conf.json](web/proxy.conf.json) to forward `/api` and WebSocket `/hubs` to the API.

**Development behavior:**

- OTP SMS is printed to the API console (`ConsoleSmsSender`).
- Turnstile uses `FakeCaptchaVerifier` when not configured for production.
- Object storage uses in-memory fake storage when `Bucket__Endpoint` is unset.

There is no `docker-compose` in this repository.

### Option 2 — Production Docker image

Build context: repository root. Image: [api/Dockerfile](api/Dockerfile).

1. **web-build** — Node 22: `npm ci`, `npm run build`
2. **api-build** — .NET SDK 10: publish API; copy SPA into `wwwroot`
3. **final** — ASP.NET runtime 10, exposes port **8080**

```bash
docker build -f api/Dockerfile -t amanah .
```

Run with production environment variables and `ConnectionStrings__Default` pointing at PostgreSQL. The API binds `0.0.0.0:$PORT` when not in Development (Render sets `PORT`).

---

## Configuration

Production settings are validated at startup where configured (for example `Bucket__*` outside Development). See [docs/deployment.md](docs/deployment.md) for the full production matrix.

### `appsettings.json` structure

Key sections in [api/appsettings.json](api/appsettings.json):

```jsonc
{
  "Database": { "AutoMigrate": true },
  "ConnectionStrings": { "Default": "" },  // set in Development / env in production
  "Jwt": {
    "AccessTokenSigningKey": "",
    "HandoffTokenSigningKey": "",
    "AccessTokenLifetimeMinutes": 15,
    "RefreshTokenLifetimeDays": 30
  },
  "Otp": { /* cooldown, limits, outbox poll */ },
  "Cors": { "AllowedOrigins": [] },
  "Bucket": { "Endpoint", "AccessKey", "SecretKey", "Name" },
  "Sms": { "ApiKey": "" },
  "Email": { /* Brevo + outbox */ },
  "RateLimit": { "Policies": { /* otp-send, auth-login, photo-upload, chat-message, ... */ } }
}
```

### Environment variable overrides

ASP.NET Core maps `__` to `:` in environment variable names:

```env
ConnectionStrings__Default=Host=...
Jwt__AccessTokenSigningKey=...
Turnstile__SecretKey=...
Cors__AllowedOrigins__0=https://your-origin.example
```

| Variable | Required (production) | Purpose |
| -------- | --------------------- | ------- |
| `ASPNETCORE_ENVIRONMENT` | Yes | `Production` on Render — JSON logs, SPA static files, real SMS/Turnstile |
| `ConnectionStrings__Default` | Yes | PostgreSQL (Supabase session pooler on Render) |
| `Jwt__AccessTokenSigningKey` | Yes | JWT signing (≥32 characters) |
| `Jwt__HandoffTokenSigningKey` | Yes | OTP handoff token (≥32 characters) |
| `Cors__AllowedOrigins__0` | Yes | Public origin (must match the browser URL) |
| `Sms__ApiKey` | Yes | Unimtx AccessKey ID |
| `Turnstile__SecretKey` | Yes | Turnstile server secret (must match the widget site key) |
| `ADMIN_PHONE` / `ADMIN_PASSWORD` | Yes | Bootstrap admin |
| `Bucket__Endpoint`, `Bucket__AccessKey`, `Bucket__SecretKey`, `Bucket__Name` | Yes* | Cloudflare R2 |
| `Bucket__PublicBaseUrl` | No | Public `r2.dev`/custom domain for thumbnails; else API uses 12h presigned URLs |
| `Email__ApiKey`, `Email__FromAddress`, `Email__FromName`, `Email__AdminAlertTo` | Optional | Brevo admin alerts |
| `USER1_PHONE`, `USER2_PHONE`, `USER_PASSWORD` | No | Optional bootstrap users; both users share `USER_PASSWORD` |

\*When `Bucket__Endpoint` is unset, the API uses in-memory storage (local dev and tests only).

### Turnstile site key (frontend)

Set `turnstileSiteKey` in [web/src/environments/environment.production.ts](web/src/environments/environment.production.ts). It is a **public** widget key baked into the SPA at `ng build` time and must pair with `Turnstile__SecretKey` on the API.

---

## API Documentation

All HTTP endpoints are versioned under **`/api/v1/`**. Interactive docs:

- **Local:** [http://localhost:5000/swagger](http://localhost:5000/swagger) (OpenAPI JSON at `/swagger/v1/swagger.json`)
- **Production:** [https://amanah-egh5.onrender.com/swagger](https://amanah-egh5.onrender.com/swagger)

Use **Authorize** in Swagger UI with `Bearer <access_token>` for protected routes. Error envelope and codes: [specs/00-api-conventions.md](specs/00-api-conventions.md).

### Authentication

Access token: **`Authorization: Bearer <jwt>`** (default lifetime 15 minutes).

Refresh token: httpOnly cookie **`amanah_refresh`**, path `/api/v1/auth`, `SameSite=Lax`, secure outside Development ([api/Auth/RefreshTokenCookieManager.cs](api/Auth/RefreshTokenCookieManager.cs)). The SPA sends `withCredentials: true` on API calls so the refresh cookie is included on `POST /api/v1/auth/refresh`.

```http
POST /api/v1/auth/login
Content-Type: application/json

{
  "phone": "+201012345678",
  "password": "...",
  "captchaToken": "..."
}
```

Response includes access token fields consumed by the Angular `AuthService`; refresh is set via `Set-Cookie`.

| Step | Method | Path |
| ---- | ------ | ---- |
| Send OTP | `POST` | `/api/v1/auth/otp/send` |
| Verify OTP | `POST` | `/api/v1/auth/otp/verify` |
| Register | `POST` | `/api/v1/auth/register` |
| Login | `POST` | `/api/v1/auth/login` |
| Refresh | `POST` | `/api/v1/auth/refresh` |
| Logout | `POST` | `/api/v1/auth/logout` |
| Logout everywhere | `POST` | `/api/v1/auth/logout-everywhere` |
| Current user | `GET` | `/api/v1/auth/me` |

### Key endpoints

| Method | Path | Description |
| ------ | ---- | ----------- |
| `GET` | `/api/v1/categories`, `/api/v1/governorates` | Catalog |
| `GET` | `/api/v1/reports` | Public browse (paginated, filtered) |
| `GET` | `/api/v1/lost/{id}`, `/api/v1/found/{id}` | Public detail |
| `POST` | `/api/v1/reports` | Create report (multipart) |
| `GET` | `/api/v1/reports/mine` | Reporter's reports |
| `POST` | `/api/v1/reports/{id}/claims` | Submit claim |
| `GET` | `/api/v1/claims/mine` | Claimant list |
| `POST` | `/api/v1/claims/{id}/approve` | Reporter approves claim |
| `GET` | `/api/v1/chats` | My chat threads |
| `POST` | `/api/v1/chats/{threadId}/messages` | Send message (REST) |
| `GET` | `/api/v1/notifications` | In-app notifications |
| `GET` | `/health`, `/health/ready` | Liveness / readiness |

Admin routes (require admin role): `/api/v1/admin/moderation/*`, `/api/v1/admin/categories/*`, `/api/v1/admin/abuse/*`, `/api/v1/admin/users/*`, `/api/v1/admin/investigations/*`, `/api/v1/admin/reports/{id}/takedown`.

### Realtime chat (SignalR)

Hub path: **`/hubs/chat`** ([contracts/Chats/ChatHubContract.cs](contracts/Chats/ChatHubContract.cs)). Contract and events: [specs/05-signalr-contract.md](specs/05-signalr-contract.md). REST chat endpoints remain available as fallback.

### SPA routes (reference)

| Area | Paths |
| ---- | ----- |
| Public | `/`, `/browse`, `/lost/{id}`, `/found/{id}`, `/terms`, `/privacy`, `/safety`, `/support` |
| Auth | `/login`, `/account/reactivate` |
| User | `/report/lost`, `/report/found`, `/my/reports`, `/my/claims`, `/my/chats`, `/notifications`, `/settings/account` |
| Admin | `/admin/moderation`, `/admin/abuse`, `/admin/users`, `/admin/categories` |

---

## Testing

| Project | Scope | Tools |
| ------- | ----- | ----- |
| `api.Tests` | HTTP stack, auth, reports, claims, chat, lifecycle, abuse, permissions | xUnit, Testcontainers (`postgres:16`), `WebApplicationFactory` |

### Run all tests

```bash
dotnet test api.Tests/Amanah.Api.Tests.csproj
```

Docker must be running. The web package has no `test` script in [web/package.json](web/package.json).

---

## Automation

[`.github/workflows/keepalive.yml`](.github/workflows/keepalive.yml) runs on a schedule (every 10 minutes) and on manual dispatch. It pings `GET /health` on the configured origin to reduce Render free-tier spin-down. There is no separate build/test workflow in this repository.

---

## Project Structure

```text
Amanah/
├── api/
│   ├── Controllers/           # Versioned REST API
│   ├── Services/              # Domain/feature services (reports, claims, chat, moderation, …)
│   ├── Data/                  # AppDbContext, configurations, migrations, seeds
│   ├── Auth/                  # JWT, refresh cookie, authorization policies
│   ├── Extensions/            # DI composition, pipeline, SignalR
│   ├── Observability/         # Health checks, metrics, request logging
│   ├── Options/               # Strongly typed configuration
│   ├── Validators/            # FluentValidation
│   ├── Dockerfile             # Production image (API + SPA)
│   └── Program.cs
├── web/
│   ├── src/app/               # Feature modules (auth, browse, reports, claims, chats, admin, …)
│   ├── src/assets/i18n/ar/    # Arabic translations (incl. error codes)
│   ├── src/environments/      # API base URL, Turnstile site key (production)
│   └── proxy.conf.json        # Dev proxy to API
├── contracts/                 # Shared DTOs and SignalR contract constants
├── api.Tests/                 # Integration and unit tests
├── specs/                     # Product spec and feature notes
├── docs/                      # deployment.md, observability.md
├── Directory.Build.props
├── Directory.Packages.props
└── .env.example
```

---

## API Response Contract

Every error from the API uses a **flat JSON envelope** (not RFC 9457 Problem Details). Full reference: [specs/00-api-conventions.md](specs/00-api-conventions.md).

### Success responses

Successful responses return the documented HTTP status and a resource JSON body (not wrapped). Many command endpoints return **204 No Content**; creates often return **200** with `{ id, status }`. Paginated lists use `items`, `page`, `pageSize`, `totalCount`, `totalPages`.

### Error responses

**Shape:**

```json
{
  "code": "validation.failed",
  "message": "Please correct the errors in the form.",
  "errors": { "displayName": ["Display name is required."] }
}
```

| Field | Description |
| ----- | ----------- |
| `code` | Stable identifier for i18n / branching |
| `message` | English summary |
| `errors` | Optional camelCase field keys → message arrays |

Common HTTP statuses: **400** validation, **401** missing/invalid token, **403** forbidden/banned/deactivated, **404** not found, **409** conflict, **410** permanently unavailable listing, **429** rate limit or quota (may include `Retry-After`), **503** dependency down, **500** unexpected fault.

### Front-end integration guide

#### 1. Authentication flow

The Angular app stores the access token in memory/session via `AuthService` and attaches `Authorization: Bearer …` on `/api/v1` requests. The auth interceptor ([web/src/app/interceptors/auth.interceptor.ts](web/src/app/interceptors/auth.interceptor.ts)) sets `withCredentials: true` and retries once on **401** by calling `POST /api/v1/auth/refresh`, then replays the request with the new access token.

#### 2. Consuming paginated lists

Browse uses `GET /api/v1/reports?page=&pageSize=` (defaults: page 1, size 20, max 50). Parse `items` and pagination fields from the JSON body. See [specs/00-api-conventions.md](specs/00-api-conventions.md#pagination).

#### 3. Unified error handler

Map `error.error.code` to Arabic copy in `web/src/assets/i18n/ar/errors.json` using `ApiErrorService` ([web/src/app/i18n/api-error.service.ts](web/src/app/i18n/api-error.service.ts)). Use `errors` for inline form field messages.

#### 4. Real-time chat (SignalR)

Connect to `/hubs/chat` with the access token; hub methods and events are defined in [specs/05-signalr-contract.md](specs/05-signalr-contract.md). In development, the Angular proxy enables WebSockets on `/hubs`.

#### 5. TypeScript error stub

```ts
interface ApiErrorBody {
  code: string;
  message: string;
  errors?: Record<string, string[]>;
}
```

---

## Design Decisions

**Central Package Management** — NuGet versions live in [Directory.Packages.props](Directory.Packages.props). Projects reference packages without per-project version attributes.

**Contracts at the boundary** — Request/response types in [contracts/](contracts/) keep HTTP contracts stable without exposing EF entities.

**Refresh cookie + short-lived JWT** — Access tokens stay in memory on the client; refresh tokens are httpOnly cookies scoped to `/api/v1/auth`, reducing XSS exposure compared to storing refresh tokens in `localStorage`.

**Outbox for side effects** — OTP SMS and admin alert email are persisted in outbox tables and processed asynchronously so API requests are not blocked on external provider latency or transient failures.

**Single deployable unit** — Production serves the Angular build from the API container’s `wwwroot`, one origin for cookies, CORS, and Turnstile.

**Authoritative storage** — R2 configuration is required outside Development (`ValidateOnStart` on bucket options). Tests and local dev use in-memory fake storage when the endpoint is unset.

**Phone normalization** — Egyptian mobile numbers are validated and normalized with `libphonenumber-csharp`, not hand-rolled regex.

**Concrete domain services** — Core business services are concrete classes registered in DI; interfaces are reserved for external systems, registries (`ILifecycleJob`), and test boundaries.

---

## Deployment

| Component | Provider |
| --------- | -------- |
| Web service (API + SPA) | Render (Docker, [api/Dockerfile](api/Dockerfile)) |
| PostgreSQL | Supabase |
| Media | Cloudflare R2 |
| SMS | Unimtx |
| Email | Brevo (optional until alerts are needed) |

Render: health check `/health`, Dockerfile path `api/Dockerfile`, build context at repo root. No `render.yaml` — configure in the Render dashboard.

After deploy, verify `/health`, `/health/ready`, sign-in, and core flows in [docs/deployment.md](docs/deployment.md).

**Production operations:** JSON logs, lifecycle jobs and outbox processors inside the API process, rate limits, and readiness checks — [docs/observability.md](docs/observability.md).
