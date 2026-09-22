# MechanicShop API

A backend REST API for managing a mechanic shop's daily operations — work orders, customers, repair tasks, invoicing, and labor scheduling. Built for shop managers and mechanics who need a structured way to track vehicles coming in, assign work, and generate invoices.

---

## Table of Contents

- [MechanicShop API](#mechanicshop-api)
    - [Table of Contents](#table-of-contents)
    - [Features](#features)
    - [Tech Stack](#tech-stack)
    - [Architecture](#architecture)
        - [Key patterns](#key-patterns)
    - [Getting Started](#getting-started)
        - [Prerequisites](#prerequisites)
        - [Clone](#clone)
        - [Environment setup](#environment-setup)
        - [Database migrations](#database-migrations)
    - [Running the Project](#running-the-project)
        - [Option 1 — Docker Compose (recommended)](#option-1--docker-compose-recommended)
        - [Option 2 — dotnet run](#option-2--dotnet-run)
    - [Configuration](#configuration)
        - [`appsettings.json` structure](#appsettingsjson-structure)
        - [Docker / environment variable overrides](#docker--environment-variable-overrides)
    - [API Documentation](#api-documentation)
        - [Authentication](#authentication)
        - [Key endpoints](#key-endpoints)
    - [Testing](#testing)
        - [Run all tests](#run-all-tests)
        - [Run a specific project](#run-a-specific-project)
    - [CI/CD](#cicd)
    - [Project Structure](#project-structure)
    - [API Response Contract](#api-response-contract)
        - [Success responses](#success-responses)
        - [Error responses — Problem Details (RFC 9457)](#error-responses--problem-details-rfc-9457)
            - [Shape](#shape)
            - [HTTP status → error kind mapping](#http-status--error-kind-mapping)
            - [Validation errors (400)](#validation-errors-400)
            - [Common error examples](#common-error-examples)
        - [Front-end integration guide](#front-end-integration-guide)
            - [1. Authentication flow](#1-authentication-flow)
            - [2. Consuming paginated lists](#2-consuming-paginated-lists)
            - [3. Unified error handler](#3-unified-error-handler)
            - [4. Real-time work order updates (SignalR)](#4-real-time-work-order-updates-signalr)
            - [5. Downloading a PDF invoice](#5-downloading-a-pdf-invoice)
            - [6. TypeScript type stubs](#6-typescript-type-stubs)
    - [Design Decisions](#design-decisions)
    - [License](#license)

---

## Features

- **Work order lifecycle** — create, assign to a shop spot (A–D), advance through Scheduled → InProgress → Completed → Cancelled states, and relocate between spots
- **Labor assignment** — assign a mechanic (labor) to a work order; only the assigned labor can update it (enforced via authorization policy)
- **Repair task catalogue** — define reusable repair tasks with labour cost, duration, and parts; attach them to work orders
- **Invoicing** — issue and settle invoices for completed work orders; download as PDF (generated via QuestPDF)
- **Customer & vehicle management** — create customers with their vehicles; phone number validated by region (libphonenumber)
- **Authentication** — email/password login with JWT access tokens + rotating refresh tokens per device session; token fingerprinting prevents token theft
- **Role-based access control** — `Manager` role required for create/update/delete operations; `Labor` role for read and self-service updates
- **Real-time updates** — SignalR hub (`/hubs/workorders`) pushes work order state changes to connected clients
- **Dashboard** — today's work order statistics endpoint
- **Background cleanup** — `OverdueBookingCleanupService` periodically cancels overdue scheduled work orders using a distributed app-lock to prevent double-runs across replicas
- **Output caching & rate limiting** — read endpoints are cached per authenticated user; write, auth, refresh, and PDF export endpoints have independent rate-limit policies
- **Observability** — structured logging via Serilog → Seq; traces and metrics exported via OpenTelemetry (OTLP) to Prometheus/Grafana

---

## Tech Stack

| Layer            | Technology                                              |
| ---------------- | ------------------------------------------------------- |
| Runtime          | .NET 10                                                 |
| Web framework    | ASP.NET Core 10                                         |
| ORM              | Entity Framework Core 10 (SQL Server)                   |
| CQRS / Mediator  | MediatR 14                                              |
| Validation       | FluentValidation 12                                     |
| Authentication   | ASP.NET Core Identity + JWT Bearer                      |
| Real-time        | SignalR                                                 |
| PDF generation   | QuestPDF 2026                                           |
| Email            | SendGrid                                                |
| Logging          | Serilog + Seq                                           |
| Observability    | OpenTelemetry (traces + metrics) + Prometheus + Grafana |
| Caching          | ASP.NET Core Output Cache + `IHybridCache`              |
| Containerisation | Docker + Docker Compose                                 |
| Testing          | xUnit, NSubstitute, Testcontainers (MsSql)              |
| API docs         | Scalar UI + Swagger UI (development)                    |

---

## Architecture

The solution uses **Clean Architecture** with four layers enforced by project references:

```text
Domain          ← no dependencies on other layers
Application     ← depends on Domain
Infrastructure  ← depends on Application + Domain
Api             ← depends on all layers (composition root)
```

### Key patterns

- **CQRS via MediatR** — every feature is a `IRequest` command or query; handlers live in `Application/Features/<Feature>/`
- **Pipeline behaviours** — `ValidationBehaviour` → `LoggingBehavior` → `PerformanceBehaviour` → `TransactionBehaviour` → `CachingBehaviour` wrap every handler automatically
- **Result pattern** — handlers return `Result<T>` instead of throwing exceptions; controllers call `.Match(Ok, Problem)` to convert results to HTTP responses
- **Domain events** — work order state transitions raise domain events (e.g. `WorkOrderChangedEvent`) consumed by infrastructure handlers
- **Strongly-typed IDs** — aggregate roots use `Guid` identity properties exposed only through factory methods
- **Contracts project** — request/response DTOs used at the API boundary live in `MechanicShop.Contracts`, keeping the API surface decoupled from domain models

---

## Getting Started

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- [Docker Desktop](https://www.docker.com/products/docker-desktop/) (for the full stack) **or** a local SQL Server instance
- (Optional) [Seq](https://datalust.co/seq) for structured log viewing

### Clone

```bash
git clone https://github.com/MohamedMamdoouh/MechanicShop.git
cd MechanicShop
```

### Environment setup

For Docker Compose, create a `.env` file in the project root with the following values:

```env
SA_PASSWORD=YourStrong!Passw0rd
JWT_SECRET_KEY=your-secret-signing-key-at-least-32-chars
JWT_ISSUER=MechanicShop
JWT_AUDIENCE=MechanicShop
TOKEN_FINGERPRINT_SALT=some-random-salt
GF_SECURITY_ADMIN_PASSWORD=admin
```

For local development without Docker, add the same values as **user-secrets**:

```bash
cd src/MechanicShop.Api
dotnet user-secrets set "JwtSettings:SecretKey" "your-secret-signing-key-at-least-32-chars"
dotnet user-secrets set "JwtSettings:Issuer" "MechanicShop"
dotnet user-secrets set "JwtSettings:Audience" "MechanicShop"
dotnet user-secrets set "TokenSettings:FingerprintSalt" "some-random-salt"
dotnet user-secrets set "SendGridSettings:ApiKey" "SG.your_key"
dotnet user-secrets set "SendGridSettings:FromEmail" "no-reply@yourshop.com"
dotnet user-secrets set "SendGridSettings:FromName" "Mechanic Shop"
dotnet user-secrets set "SendGridSettings:TemplateId" "d-your-template-id"
dotnet user-secrets set "ConnectionStrings:DefaultConnection" \
  "Server=localhost\\SQLEXPRESS;Database=MechanicShop;Trusted_Connection=True;TrustServerCertificate=True;"
```

### Database migrations

Migrations run automatically in `Development` when the app starts (`InitialiseDatabaseAsync`). To apply them manually:

```bash
dotnet ef database update \
  --project src/MechanicShop.Infrastructure \
  --startup-project src/MechanicShop.Api
```

---

## Running the Project

### Option 1 — Docker Compose (recommended)

Starts the API, SQL Server 2022, Seq, Prometheus, and Grafana:

```bash
docker-compose up --build
```

| Service    | URL                               |
| ---------- | --------------------------------- |
| API        | <http://localhost:5001>           |
| Scalar UI  | <http://localhost:5001/scalar/v1> |
| Swagger UI | <http://localhost:5001/swagger>   |
| Seq (logs) | <http://localhost:8081>           |
| Prometheus | <http://localhost:9090>           |
| Grafana    | <http://localhost:3000>           |

### Option 2 — dotnet run

```bash
cd src/MechanicShop.Api
dotnet run
```

The API will be available at `https://localhost:7xxx` / `http://localhost:5xxx` (ports printed on startup). Interactive API docs open at `/scalar/v1` or `/swagger`.

---

## Configuration

All settings are validated at startup (`ValidateOnStart`). The app will refuse to start if a required value is missing.

### `appsettings.json` structure

```jsonc
{
    "ConnectionStrings": {
        "DefaultConnection": "", // SQL Server connection string
    },
    "JwtSettings": {
        "SecretKey": "", // REQUIRED — min 32 chars recommended
        "Issuer": "", // REQUIRED
        "Audience": "", // REQUIRED
        "AccessTokenExpiryMinutes": 15,
        "RefreshTokenExpiryDays": 7,
    },
    "TokenSettings": {
        "FingerprintSalt": "", // REQUIRED — used to hash the token fingerprint cookie
    },
    "SendGridSettings": {
        "ApiKey": "", // REQUIRED
        "FromEmail": "", // REQUIRED
        "FromName": "", // REQUIRED
        "TemplateId": "", // REQUIRED — dynamic template ID
    },
    "AppSettings": {
        "OpeningTime": "08:00",
        "ClosingTime": "18:00",
        "MaxSpots": 4,
        "ShopTimeZone": "Africa/Cairo", // IANA timezone identifier
        "CorsPolicyName": "MechanicShopCorsPolicy",
        "CorsAllowedOrigins": ["https://localhost:7102"],
        // ... see AppSettings.cs for full list
    },
}
```

### Docker / environment variable overrides

ASP.NET Core maps `__` to `:` in environment variable names:

```env
JwtSettings__SecretKey=...
ConnectionStrings__DefaultConnection=...
```

---

## API Documentation

All endpoints are versioned under `/api/v1/`. The full interactive spec is available at `/scalar/v1` (development).

### Authentication

```http
POST /api/v1/identity/login
Content-Type: application/json

{
  "email": "manager@shop.com",
  "password": "P@ssword1",
  "deviceIdentifier": "web-browser-abc"
}
```

Response: `{ "accessToken": "...", "refreshToken": "..." }`

Use the access token as a Bearer header on all subsequent requests.

### Key endpoints

| Method  | Path                             | Role          | Description                     |
| ------- | -------------------------------- | ------------- | ------------------------------- |
| `POST`  | `/api/v1/identity/login`         | —             | Authenticate and obtain tokens  |
| `POST`  | `/api/v1/identity/refresh`       | —             | Rotate access + refresh tokens  |
| `GET`   | `/api/v1/identity/me`            | Any           | Current user info               |
| `GET`   | `/api/v1/workorders`             | Any           | Paginated, filtered work orders |
| `POST`  | `/api/v1/workorders`             | Manager       | Create a work order             |
| `PATCH` | `/api/v1/workorders/{id}/state`  | Labor/Manager | Advance work order state        |
| `PATCH` | `/api/v1/workorders/{id}/assign` | Manager       | Assign labor to work order      |
| `GET`   | `/api/v1/invoices/{id}`          | Any           | Get invoice details             |
| `GET`   | `/api/v1/invoices/{id}/pdf`      | Any           | Download invoice as PDF         |
| `POST`  | `/api/v1/invoices`               | Manager       | Issue invoice for a work order  |
| `PATCH` | `/api/v1/invoices/{id}/settle`   | Manager       | Mark invoice as paid            |
| `GET`   | `/api/v1/customers`              | Any           | List all customers              |
| `POST`  | `/api/v1/customers`              | Manager       | Create a customer               |
| `GET`   | `/api/v1/repairtasks`            | Any           | List repair task catalogue      |
| `POST`  | `/api/v1/repairtasks`            | Manager       | Add a repair task               |
| `GET`   | `/api/v1/dashboard/today-stats`  | Any           | Today's work order counts       |
| `GET`   | `/api/settings`                  | —             | Public shop configuration       |

---

## Testing

The solution has four test projects with different scopes:

| Project                                      | Scope                                            | Tools                                          |
| -------------------------------------------- | ------------------------------------------------ | ---------------------------------------------- |
| `MechanicShop.Domain.UnitTests`              | Domain logic, value objects, state machines      | xUnit, NSubstitute                             |
| `MechanicShop.Application.UnitTests`         | Pipeline behaviours, mappers                     | xUnit, NSubstitute                             |
| `MechanicShop.Application.SubcutaneousTests` | Full application layer against real SQL Server   | xUnit, Testcontainers                          |
| `MechanicShop.Api.IntegrationTests`          | Full HTTP stack including auth and rate limiting | xUnit, Testcontainers, `WebApplicationFactory` |

### Run all tests

```bash
dotnet test
```

### Run a specific project

```bash
dotnet test tests/MechanicShop.Domain.UnitTests
dotnet test tests/MechanicShop.Application.UnitTests
dotnet test tests/MechanicShop.Application.SubcutaneousTests
dotnet test tests/MechanicShop.Api.IntegrationTests
```

Subcutaneous and integration tests spin up a real SQL Server container via Testcontainers — Docker must be running.

---

## CI/CD

GitHub Actions runs on every push to `master` and on pull requests (`.github/workflows/build-and-test.yml`).

**Job 1 — Build & Unit Tests** (`ubuntu-latest`, no Docker required)

1. Restore → Build (Release)
2. Run `Domain.UnitTests` and `Application.UnitTests`
3. Upload `.trx` results as artifacts

**Job 2 — Integration & Subcutaneous Tests** (depends on Job 1, Docker available)

1. Restore → Build (Release)
2. Run `Application.SubcutaneousTests` and `Api.IntegrationTests` against Testcontainers SQL Server
3. Upload `.trx` results as artifacts

The split keeps fast feedback (unit tests) decoupled from the slower container-based tests.

---

## Project Structure

```text
MechanicShop/
├── src/
│   ├── MechanicShop.Api/               # Composition root: controllers, middleware, DI wiring
│   │   ├── Controllers/                # One controller per resource; thin — only map to commands/queries
│   │   ├── Infrastructure/             # Middleware (request logging context, exception handler)
│   │   ├── OpenApi/                    # Document + operation transformers for Scalar/Swagger
│   │   └── Services/                   # IUser implementation (CurrentUser)
│   │
│   ├── MechanicShop.Application/       # Use cases, no framework dependencies
│   │   ├── Common/
│   │   │   ├── Behaviours/             # MediatR pipeline: validation, logging, perf, tx, cache
│   │   │   ├── Interfaces/             # IAppDbContext, IUser, ITokenProvider, etc.
│   │   │   └── Models/                 # Shared DTOs (PaginatedList, Result<T>, TokenSettings)
│   │   └── Features/                   # Vertical slices: Billing, Customer, Dashboard, Identity,
│   │                                   # Labor, RepairTasks, Scheduling, WorkOrder
│   │
│   ├── MechanicShop.Domain/            # Enterprise business rules, no infrastructure concerns
│   │   ├── Common/                     # Base entities, Result type, Error type
│   │   ├── Customers/                  # Customer + Vehicle aggregates
│   │   ├── Employees/                  # Employee aggregate
│   │   ├── Identity/                   # RefreshToken, Role, DeviceInfo value objects
│   │   ├── RepairTasks/                # RepairTask aggregate (parts, labour cost)
│   │   └── WorkOrder/                  # WorkOrder aggregate, Invoice, domain events, enums
│   │
│   ├── MechanicShop.Contracts/         # API request/response types (no domain types exposed)
│   │
│   └── MechanicShop.Infrastructure/    # EF Core, Identity, SignalR, SendGrid, background jobs
│       ├── Data/                       # AppDbContext, entity configurations, interceptors
│       ├── Identity/                   # IdentityService, TokenProvider, AppUser
│       ├── Migrations/                 # EF Core migration history
│       ├── Realtime/                   # WorkOrderHub (SignalR)
│       ├── BackgroundJobs/             # OverdueBookingCleanupService
│       └── Services/                   # PDF generator, notification service, phone validator
│
├── tests/
│   ├── MechanicShop.Domain.UnitTests/
│   ├── MechanicShop.Application.UnitTests/
│   ├── MechanicShop.Application.SubcutaneousTests/
│   ├── MechanicShop.Api.IntegrationTests/
│   └── MechanicShop.Tests.Common/      # Shared fakes (FakeTimeProvider, builders, etc.)
│
├── containers/
│   ├── prometheus/prometheus.yml       # Prometheus scrape config
│   └── seq/                            # Seq persistent data volume
│
├── requests/                           # .http files for manual API testing in VS Code / Rider
├── docker-compose.yml
├── Dockerfile
├── Directory.Build.props               # Solution-wide: TargetFramework, Nullable, Analyzers
└── Directory.Packages.props            # Central Package Management — single source of truth for versions
```

---

## API Response Contract

Every response from this API — success or failure — follows a consistent, predictable contract. This section explains the exact shapes so front-end developers know exactly what to expect before writing a single line of client code.

---

### Success responses

Successful responses return the HTTP status code documented on each endpoint and a JSON body that is the DTO for that resource.

| Scenario                    | Status           | Body                                  |
| --------------------------- | ---------------- | ------------------------------------- |
| Resource returned           | `200 OK`         | DTO object or paginated list          |
| Resource created            | `201 Created`    | Newly created DTO + `Location` header |
| Command succeeded (no body) | `204 No Content` | _(empty)_                             |

**Example — get a customer (200):**

```json
{
    "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "firstName": "Ahmed",
    "lastName": "Hassan",
    "email": "ahmed@example.com",
    "phoneNumber": "+201012345678",
    "vehicles": [
        {
            "vehicleId": "...",
            "make": "Toyota",
            "model": "Corolla",
            "year": 2022,
            "licensePlate": "ABC-1234"
        }
    ]
}
```

**Example — create customer (201):**

```http
HTTP/1.1 201 Created
Location: /api/v1/customers/3fa85f64-5717-4562-b3fc-2c963f66afa6

{ ...customer DTO... }
```

**Example — paginated list (200):**

```json
{
    "items": [{ "...": "..." }, { "...": "..." }],
    "pageNumber": 1,
    "pageSize": 20,
    "totalCount": 67,
    "totalPages": 4,
    "hasPreviousPage": false,
    "hasNextPage": true
}
```

---

### Error responses — Problem Details (RFC 9457)

All error responses follow [RFC 9457 Problem Details for HTTP APIs](https://www.rfc-editor.org/rfc/rfc9457). The `Content-Type` is always `application/problem+json`.

#### Shape

```json
{
    "type": "string",
    "title": "string",
    "status": 400,
    "detail": "string (optional — extra context)",
    "instance": "/api/v1/customers/...",
    "traceId": "00-abc123-def456-00"
}
```

| Field      | Description                                                              |
| ---------- | ------------------------------------------------------------------------ |
| `type`     | A URI-like identifier for the error class (e.g. `NotFound`, `Conflict`)  |
| `title`    | Human-readable description of what went wrong                            |
| `status`   | Mirrors the HTTP status code                                             |
| `detail`   | Optional extra context (present in Development for unhandled exceptions) |
| `instance` | The request path that triggered the error                                |
| `traceId`  | W3C trace ID — pass this to support for log correlation in Seq           |

#### HTTP status → error kind mapping

The API maps its internal `ErrorKind` domain type deterministically to HTTP status codes:

| `ErrorKind`    | HTTP status                 | When it occurs                             |
| -------------- | --------------------------- | ------------------------------------------ |
| `NotFound`     | `404 Not Found`             | Requested entity does not exist            |
| `Validation`   | `400 Bad Request`           | Input failed FluentValidation rules        |
| `Conflict`     | `409 Conflict`              | Duplicate email, spot already booked, etc. |
| `Unauthorized` | `401 Unauthorized`          | Invalid credentials / expired token        |
| `Forbidden`    | `403 Forbidden`             | Authenticated but wrong role               |
| `Unexpected`   | `500 Internal Server Error` | Unhandled domain or infrastructure error   |

#### Validation errors (400)

When one or more fields fail validation the response adds an `errors` map — one key per failing field:

```json
{
    "type": "https://tools.ietf.org/html/rfc9110#section-15.5.1",
    "title": "One or more validation errors occurred.",
    "status": 400,
    "errors": {
        "Email": ["Email address is not valid."],
        "PhoneNumber": ["Phone number must include a country code."]
    },
    "traceId": "00-abc123-def456-00"
}
```

#### Common error examples

**404 — resource not found:**

```json
{
    "type": "NotFound",
    "title": "Customer not found.",
    "status": 404,
    "instance": "/api/v1/customers/00000000-0000-0000-0000-000000000000",
    "traceId": "00-abc123-def456-00"
}
```

**409 — business rule conflict:**

```json
{
    "type": "Conflict",
    "title": "A customer with this email already exists.",
    "status": 409,
    "instance": "/api/v1/customers",
    "traceId": "00-abc123-def456-00"
}
```

**401 — unauthenticated:**

```json
{
    "type": "Unauthorized",
    "title": "Invalid credentials.",
    "status": 401,
    "instance": "/api/v1/identity/login",
    "traceId": "00-abc123-def456-00"
}
```

---

### Front-end integration guide

#### 1. Authentication flow

```
┌─────────┐   POST /api/v1/identity/login   ┌─────────┐
│  Client │ ──────────────────────────────► │   API   │
│         │ ◄────────────────────────────── │         │
│         │  { accessToken, refreshToken }  │         │
└─────────┘                                 └─────────┘
```

1. Call `POST /api/v1/identity/login` with `{ email, password, deviceIdentifier }`.
2. Store **`accessToken`** in memory (never in `localStorage` — XSS risk).
3. Store **`refreshToken`** as an `HttpOnly` cookie or in secure storage.
4. Attach the access token to every request:
    ```http
    Authorization: Bearer <accessToken>
    ```
5. When a request returns `401`, call `POST /api/v1/identity/refresh` with `{ accessToken, refreshToken }` to rotate both tokens silently.
6. Call `POST /api/v1/identity/logout` on sign-out to revoke the session server-side.

#### 2. Consuming paginated lists

```ts
// TypeScript example
interface PaginatedList<T> {
    items: T[];
    pageNumber: number;
    pageSize: number;
    totalCount: number;
    totalPages: number;
    hasPreviousPage: boolean;
    hasNextPage: boolean;
}

const response = await fetch(
    "/api/v1/workorders?pageNumber=1&pageSize=20&status=Scheduled",
    { headers: { Authorization: `Bearer ${accessToken}` } },
);
const data: PaginatedList<WorkOrderListItem> = await response.json();
```

#### 3. Unified error handler

Because every error is a Problem Details object you can write a single handler for the entire API:

```ts
async function apiFetch(url: string, options?: RequestInit) {
    const res = await fetch(url, options);

    if (res.ok) {
        // 204 No Content has no body
        return res.status === 204 ? null : res.json();
    }

    const problem = await res.json(); // always application/problem+json

    if (res.status === 400 && problem.errors) {
        // Validation failure — field-level errors
        throw new ValidationError(problem.errors);
    }

    // All other errors — show problem.title to the user
    throw new ApiError(res.status, problem.title, problem.traceId);
}
```

#### 4. Real-time work order updates (SignalR)

The hub at `/hubs/workorders` pushes state changes so the UI stays in sync without polling:

```ts
import * as signalR from "@microsoft/signalr";

const connection = new signalR.HubConnectionBuilder()
    .withUrl("/hubs/workorders", {
        accessTokenFactory: () => accessToken,
    })
    .withAutomaticReconnect()
    .build();

// Fired when any work order changes state
connection.on("WorkOrderUpdated", (workOrderId: string, newState: string) => {
    // invalidate cache / update local state
});

await connection.start();
```

#### 5. Downloading a PDF invoice

The `/api/v1/invoices/{id}/pdf` endpoint returns a binary PDF stream:

```ts
const res = await fetch(`/api/v1/invoices/${invoiceId}/pdf`, {
    headers: { Authorization: `Bearer ${accessToken}` },
});
const blob = await res.blob();
const url = URL.createObjectURL(blob);
window.open(url); // or trigger a download link
```

#### 6. TypeScript type stubs

Minimal types matching the API contract:

```ts
interface TokenResponse {
    accessToken: string;
    refreshToken: string;
}

interface ProblemDetails {
    type: string;
    title: string;
    status: number;
    detail?: string;
    instance?: string;
    traceId?: string;
    errors?: Record<string, string[]>; // present on 400 validation errors
}

interface WorkOrderListItem {
    workOrderId: string;
    spot: "A" | "B" | "C" | "D";
    state: "Scheduled" | "InProgress" | "Completed" | "Cancelled";
    startAt: string; // ISO 8601
    endAt: string;
    customerName: string;
    vehicleLicensePlate: string;
}
```

---

## Design Decisions

**Central Package Management** — all NuGet versions live in `Directory.Packages.props`. Individual projects declare `PackageReference` without a `Version` attribute. This prevents version drift across projects without a separate tool.

**Result pattern instead of exceptions** — domain and application layers return `Result<T>` (a discriminated union of success/failure). Exceptions are reserved for truly unexpected failures. This makes control flow explicit and keeps handler tests straightforward.

**Pipeline behaviours** — cross-cutting concerns (validation, logging, transactions, caching) are applied automatically to every MediatR handler via open generic behaviours registered in `AddMediatR`. Adding a new concern is a single file — no base class inheritance required.

**Token fingerprinting** — the refresh token is bound to a device fingerprint derived from the User-Agent and a server-side salt. A stolen refresh token from a different device is rejected even before expiry.

**Non-root Docker image** — the container runs as the built-in `app` user (UID 1000). Port 8080 is used instead of 443/80, which requires no elevated privileges. This follows OWASP A05 (Security Misconfiguration).

**`ValidateOnStart()` on all options** — every settings class marked with `[Required]` attributes will cause the app to fail immediately at startup if a required value is missing. This surfaces configuration errors before any traffic hits the service.

**Spot-based scheduling** — the shop has a fixed number of work bays (`Spot` enum: A, B, C, D). Scheduling logic validates that a spot is not double-booked for the requested time window inside the domain.

---

## License

[MIT](LICENSE)
