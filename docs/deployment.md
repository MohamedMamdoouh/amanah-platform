# Deployment

Production runs on a **$0/month MVP stack** (pre-launch). SMS is pay-as-you-go on top.

| Component              | Provider      | Role                        |
| ---------------------- | ------------- | --------------------------- |
| Angular SPA + .NET API | Render        | Single web service (Docker) |
| PostgreSQL             | Supabase      | Primary database            |
| Media storage          | Cloudflare R2 | Report photos               |
| SMS (OTP)              | Unimtx        | Phone verification          |
| Email (admin alerts)   | Resend        | Moderation-queue alerts     |

One public origin serves both the app and `/api/v1/*`.

---

## Setup order

1. **Supabase** — create project and database
2. **Render** — deploy the Docker web service (see below)
3. **Cloudflare R2** — create bucket and credentials for media
4. **Unimtx** — create account, add credit, configure SMS API key
5. **Resend** (optional until staging) — create account, verify domain (or use `onboarding@resend.dev`), configure admin alert email
6. **Keepalive** (optional) — scheduled ping to avoid free-tier spin-down

Verify `/health`, `/health/ready`, the home page, sign-in/sign-up, report submission, and claim submit/review flows after deploy (see [specs/04-claims-verification.md](../specs/04-claims-verification.md) §9 manual smoke).

See [observability.md](observability.md) for logs, metrics, and alerting.

---

## Render web service

| Setting | Value |
| ------- | ----- |
| Type | Web Service |
| Environment | Docker |
| Dockerfile path | `api/Dockerfile` |
| Build context | Repository root |
| Health check path | `/health` |

The Angular production Turnstile **site key** is committed in `web/src/environments/environment.production.ts` (public key; must match the Turnstile widget for `Turnstile__SecretKey`).

The API binds `0.0.0.0:$PORT` (Render sets `PORT` automatically). EF Core migrations run on startup (`Database:AutoMigrate` defaults `true`).

---

## Required production environment variables

See `.env.example` for naming reference. Double-underscore maps to nested config (`ConnectionStrings__Default` → `ConnectionStrings:Default`).

| Variable | Required | Purpose |
| -------- | -------- | ------- |
| `ASPNETCORE_ENVIRONMENT` | Yes | `Production` on Render — JSON logs, SPA static files, and real SMS/Turnstile. `Development` uses console SMS, a fake captcha, and in-memory storage when the bucket is unset |
| `ConnectionStrings__Default` | Yes | Supabase Postgres — **Session pooler** on Render (`aws-0-<region>.pooler.supabase.com`, user `postgres.<ref>`); direct connection for local dev |
| `Jwt__AccessTokenSigningKey` | Yes | JWT signing (≥32 chars) |
| `Jwt__HandoffTokenSigningKey` | Yes | OTP handoff token (≥32 chars) |
| `Cors__AllowedOrigins__0` | Yes | Public origin (e.g. `https://<service>.onrender.com`) |
| `Sms__ApiKey` | Yes | Unimtx AccessKey ID |
| `Turnstile__SecretKey` | Yes | Cloudflare Turnstile server secret |
| `ADMIN_PHONE` | Yes | Bootstrap admin phone (`+20...`) |
| `ADMIN_PASSWORD` | Yes | Bootstrap admin password (≥8 chars) |
| `SEED_USER_PHONE` | No | Optional bootstrap normal user for staging/dev (`+20...`); omit in production |
| `SEED_USER_PASSWORD` | No | Optional bootstrap normal user password (≥8 chars); omit in production |
| `Bucket__Endpoint` | Yes* | R2 S3 API endpoint |
| `Bucket__AccessKey` | Yes* | R2 access key ID |
| `Bucket__SecretKey` | Yes* | R2 secret access key |
| `Bucket__Name` | Yes* | Bucket name (e.g. `amanah-media`) |
| `Email__ApiKey` | Optional pre-staging | Resend API key |
| `Email__FromAddress` | Optional pre-staging | Verified sender |
| `Email__AdminAlertTo` | Optional pre-staging | Admin inbox for moderation alerts |

\*When `Bucket__Endpoint` is unset, the API uses in-memory fake storage — suitable for local dev and tests, not production.

`Email__AppBaseUrl` defaults to the first `Cors__AllowedOrigins` entry when unset.

---

## Lifecycle jobs (Phase 06)

Background work runs inside the API process as separate hosted services:

- `LifecycleJobsHostedService` polls the lifecycle jobs below (default every `Lifecycle__JobsPollIntervalSeconds`, 3600).
- `OtpSmsOutboxProcessor` polls the OTP SMS outbox (`Otp__OutboxPollIntervalSeconds`, default 30).
- `AdminAlertEmailOutboxProcessor` polls admin alert email (`Email__OutboxPollIntervalSeconds`, default 30).
- `StorageDeletionOutboxProcessor` polls R2 deletes (`StorageDeletion__PollIntervalSeconds`, default 10).

Admins can run one lifecycle job with `POST /api/v1/admin/test/run-job/{jobName}`. The route returns **404** when `ASPNETCORE_ENVIRONMENT` is `Production`. It stays available in Development and Staging.

Registered lifecycle jobs: `ListingExpiryWarning`, `ListingAutoExpiry`, `PendingClaimTimeout`, `RejectedReportCleanup`, `ChatRetention`, `StorageDeletionOutboxCleanup`, `OtpCleanup`, `SessionCleanup`, `OtpSmsOutboxCleanup`, `AdminAlertEmailOutboxCleanup`, `NotificationCleanup`, `OrphanedStorageCleanup`.

| Variable | Default | Purpose |
| -------- | ------- | ------- |
| `Lifecycle__ListingExpiryDays` | `90` | Cumulative published days before auto-expiry |
| `Lifecycle__ListingExpiryWarningDaysBefore` | `7` | Warning fires at `ListingExpiryDays -` this value |
| `Lifecycle__ClaimTimeoutMinutes` | `14400` (10 days) | Pending-claim auto-withdraw timeout |
| `Lifecycle__RetentionDays` | `30` | Retention for rejected reports, chat, sessions, and refresh tokens |
| `Lifecycle__NotificationRetentionDays` | `7` | In-app notifications older than this are deleted |
| `Lifecycle__JobsPollIntervalSeconds` | `3600` | Seconds between lifecycle job poll cycles |
| `StorageDeletion__PollIntervalSeconds` | `10` | Seconds between storage deletion outbox processor poll cycles |
| `StorageDeletion__BatchSize` | `50` | Max outbox rows processed per processor batch |
| `StorageDeletion__MaxAttempts` | `5` | Max R2 delete attempts before an outbox row is marked failed |

These `Lifecycle` and `StorageDeletion` values default in option classes. Override them with environment variables. Tests set values via `Lifecycle:ListingExpiryDays` and the same pattern for the other keys. `appsettings.json` does not repeat the lifecycle block.

Each job emits one **Information** completion log with its outcome (for example `WithdrawnCount`, `WarningsSent`, `ExpiredCount`). `JobRunner` also logs `Lifecycle job {JobName} completed.` Failures are logged at **Error** by `JobRunner` / `LifecycleJobsHostedService`. See [observability.md](observability.md).

---

## Cloudflare R2 (report and claim photos)

| Variable | Purpose |
| -------- | ------- |
| `Bucket__Endpoint` | R2 S3 API endpoint (`https://<account-id>.r2.cloudflarestorage.com`) |
| `Bucket__AccessKey` | R2 access key ID |
| `Bucket__SecretKey` | R2 secret access key |
| `Bucket__Name` | Bucket name (e.g. `amanah-media`) |

Photos are stored under `public/` or `private/` prefixes based on category `photosPrivate`. Report photos are uploaded with `POST /api/v1/reports` (multipart) and written directly to the report prefix on submit. Claim photos use `private/claims/{claimId}/…`, uploaded in the same multipart request as `POST /api/v1/reports/{id}/claims`, and served via short-lived presigned URLs (`GET /api/v1/uploads/claim-photo/{claimId}/url`).

**Orphaned objects:** report submit runs compensating storage delete when `SaveChangesAsync` fails after photo promotion. The daily `OrphanedStorageCleanup` lifecycle job deletes unreferenced keys under report photo prefixes (backstop for partial failures). See [specs/06-lifecycle-retention.md](../specs/06-lifecycle-retention.md#orphaned-storage-cleanup) and `OrphanedStorageTests`.

---

## Resend (admin moderation alerts)

Admin receives one email when a report enters the moderation queue (new submit or resubmit). When `Email__ApiKey` is **unset**, the API uses a no-op sender (local dev and tests).

| Variable | Purpose |
| -------- | ------- |
| `Email__ApiKey` | Resend API key (`re_...`) |
| `Email__FromAddress` | Verified sender, e.g. `Amanah <alerts@yourdomain.com>` — use `onboarding@resend.dev` until domain verified |
| `Email__AdminAlertTo` | Admin inbox (founder's email) |
| `Email__AppBaseUrl` | Optional public app URL for review links (falls back to first `Cors__AllowedOrigins` entry) |
| `Email__OutboxPollIntervalSeconds` | Background worker poll interval (default `30`) |
| `Email__OutboxMaxAttempts` | Max dispatch attempts before marking failed (default `5`) |
| `Email__OutboxBatchSize` | Messages processed per poll (default `10`) |

Alerts are written to an outbox table in the same database transaction as the report submit/resubmit, then sent asynchronously by a background worker (same pattern as OTP SMS).

Setup:

1. Create a free account at [resend.com](https://resend.com)
2. Generate an API key
3. For staging: send from `onboarding@resend.dev` (no DNS required)
4. Before public launch: verify your domain in Resend and switch `Email__FromAddress` to `@yourdomain.com`

---

## Free tier notes

| Limit                                | Mitigation                                |
| ------------------------------------ | ----------------------------------------- |
| Render spins down after ~15 min idle | Keepalive ping or paid plan before launch |
| Supabase pauses when idle            | Health checks keep the DB warm            |
| SMS is metered                       | Monitor Unimtx balance                    |

---

## Local development

See [README.md](../README.md).

---

## Pre-launch checklist

Walk this on the staging or production service before public launch. Product code through phase 07 is already in the repo; these items are environment and QA.

- [ ] `ASPNETCORE_ENVIRONMENT=Production` (JSON logs, SPA fallback, Unimtx, Turnstile)
- [ ] `ConnectionStrings__Default` uses the Supabase **Session pooler**
- [ ] JWT signing keys, `ADMIN_PHONE`, and `ADMIN_PASSWORD` set; `SEED_USER_PHONE` and `SEED_USER_PASSWORD` omitted
- [ ] `Bucket__Endpoint`, `Bucket__AccessKey`, `Bucket__SecretKey`, and `Bucket__Name` set so `/health/ready` checks R2
- [ ] `Sms__ApiKey` set and the Unimtx balance is funded
- [ ] `Turnstile__SecretKey` set and `web/src/environments/environment.production.ts` `turnstileSiteKey` matches that widget
- [ ] `Cors__AllowedOrigins__0` is the public origin; add the custom domain as another origin when DNS is live
- [ ] Custom domain configured on Render (still open — see SPEC section 14)
- [ ] Resend domain verified and `Email__FromAddress` uses that domain
- [ ] `KEEPALIVE_URL` in `.github/workflows/keepalive.yml` matches the public origin
- [ ] `GET /health` returns 200 and `GET /health/ready` is healthy
- [ ] Manual smoke from phase specs §9: [02](../specs/02-admin-moderation.md), [04](../specs/04-claims-verification.md), [05](../specs/05-chat-resolution-notifications.md), [06](../specs/06-lifecycle-retention.md)
- [ ] Flag a published listing, open `/admin/abuse`, and resolve it (no action, takedown, or ban)
