# Amanah

Lost-and-found platform for Egypt — moderated listings, ownership verification, and in-app messaging. Arabic RTL UI.

**Docs:** [SPEC](specs/SPEC.md) · [API conventions](specs/00-api-conventions.md) · [Phase specs](specs/README.md) · [Deployment](docs/deployment.md) · [Observability](docs/observability.md)

## Status

| Phase | Topic                                                       | Status       |
| ----- | ----------------------------------------------------------- | ------------ |
| 00    | Platform foundation (auth, sessions, deploy, seeds)         | **Complete** |
| 01    | Report submission                                           | **Complete** |
| 02    | Admin moderation (queue, resubmit, categories, email)       | **Complete** |
| 03    | Browse & discovery (search, filters, public detail)         | **Complete** |
| 04    | Claims & verification (submit, review, photos, My Claims)   | **Complete** |
| 05    | Chat, resolution & notifications                            | **Complete** |
| 06–07 | Lifecycle, trust & safety                                     | Not started  |

**Next up:** Phase 06 — lifecycle, retention, and account management.

### Shipped (Phase 00)

Auth (phone OTP signup, password sign-in, JWT + httpOnly refresh cookie rotation, password reset, logout-everywhere), admin bootstrap, catalog seeds (8 categories, 27 governorates), Arabic RTL SPA with legal/support pages, full DB schema, structured logging + health probes, production Docker deploy on Render.

**Routes:** `/`, `/browse`, `/login`, `/notifications`, `/terms`, `/privacy`, `/safety`, `/support`, `/admin` (redirects to moderation)

### Shipped (Phase 01)

Lost/found report submission with category fields, hidden verification detail, contact-info blocking, quotas, photo pipeline (EXIF strip, WebP, R2), and normalized search column for Phase 03 browse.

**Routes:** `/report/lost`, `/report/found`, `/my/reports`, `/my/reports/{id}`

Details: [specs/01-report-submission.md](specs/01-report-submission.md)

### Shipped (Phase 02)

Admin FIFO moderation queue (approve/reject, keyword search), reporter edit/resubmit for rejected reports, category/field CRUD, in-app notification center (`ReportApproved` / `ReportRejected`), admin alert email outbox (Resend).

**Routes:** `/admin/moderation`, `/admin/moderation/{id}`, `/admin/categories`, `/notifications` (+ My Reports Rejected/Published tabs)

Details: [specs/02-admin-moderation.md](specs/02-admin-moderation.md)

### Shipped (Phase 03)

Public browse listing with Arabic keyword search, filters, pagination, and status-aware public detail pages (`/lost/{id}`, `/found/{id}`) with not-found and permanently-unavailable routing.

**Routes:** `/browse`, `/lost/{id}`, `/found/{id}`, `/not-found`, `/unavailable`

Details: [specs/03-browse-discovery.md](specs/03-browse-discovery.md)

### Shipped (Phase 04)

Claim submission on published reports (multipart text + optional photo), daily quota and attempt limits, reporter approve/reject with auto-reject of competing claims, claimant withdraw, My Claims list, reporter claims section on report detail with presigned photos, in-app notifications for claim events. `ChatThread` rows created on approval; messaging, resolution, and My Chats UI shipped in Phase 05.

**Routes:** `/my/claims` (+ claim form on `/lost/{id}`, `/found/{id}`; claims review on `/my/reports/{id}`; `claim_in_progress` tab on `/my/reports`)

Details: [specs/04-claims-verification.md](specs/04-claims-verification.md)

### Shipped (Phase 05)

SignalR live chat with REST fallback, photo attachments, safety banner, confirm resolved / cancel claim flows, My Chats list and thread view, resolution and chat notification labels with deep links.

**Routes:** `/my/chats`, `/my/chats/{threadId}` (+ confirm/cancel on `/lost/{id}`, `/found/{id}`, `/my/reports/{id}`)

Details: [specs/05-chat-resolution-notifications.md](specs/05-chat-resolution-notifications.md)

### Not built yet

Lifecycle jobs, abuse enforcement — see [phase specs](specs/README.md).

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
| User | `01022222222` | `UserPass123` | `/report/lost`, `/report/found`, `/my/reports`, `/my/claims`, `/my/chats`, `/browse` |

## Tests

Requires **Docker** (Testcontainers). Integration tests start **PostgreSQL 16** (`postgres:16`) per test factory — same engine and Npgsql provider as local dev and Supabase production.

```bash
dotnet test api.Tests/Amanah.Api.Tests.csproj
```

## Production deploy

Single **Render** Docker service (API + Angular) + **Supabase** Postgres (Session pooler on Render) + **Cloudflare R2** + **Unimtx** SMS. Dashboard setup only — no `render.yaml`. See [docs/deployment.md](docs/deployment.md).

After deploy, verify `/health`, `/health/ready`, sign-in/sign-up, and report submission. See [docs/observability.md](docs/observability.md) for logs, metrics, and alerting.
