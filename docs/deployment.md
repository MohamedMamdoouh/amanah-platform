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
2. **Render** — deploy the Docker web service; configure secrets and connection string
3. **Cloudflare R2** — create bucket and credentials for media
4. **Unimtx** — create account, add credit, configure SMS API key
5. **Resend** (optional until staging) — create account, verify domain (or use `onboarding@resend.dev`), configure admin alert email
6. **Keepalive** (optional) — scheduled ping to avoid free-tier spin-down

Verify `/health`, `/health/ready`, the home page, sign-in/sign-up, and report submission after deploy.

See [observability.md](observability.md) for logs, metrics, and alerting.

---

## Cloudflare R2 (report photos)

Set on the Render web service (or in `.env` locally):

| Variable | Purpose |
| -------- | ------- |
| `Bucket__Endpoint` | R2 S3 API endpoint (`https://<account-id>.r2.cloudflarestorage.com`) |
| `Bucket__AccessKey` | R2 access key ID |
| `Bucket__SecretKey` | R2 secret access key |
| `Bucket__Name` | Bucket name (e.g. `amanah-media`) |

When `Bucket__Endpoint` is **unset**, the API uses an in-memory fake storage provider — suitable for local dev and tests, not for production photo persistence across restarts.

Photos are stored under `public/` or `private/` prefixes based on category `photosPrivate`. Report photos are uploaded with `POST /api/v1/reports` (multipart) and written directly to the report prefix on submit.

**Known gap:** if R2 upload succeeds but the database commit fails, promoted files are not deleted automatically today. Phase 07 will add compensating cleanup on submit failure and a scheduled `OrphanedStorageCleanup` job. See [specs/07-lifecycle-retention.md](../specs/07-lifecycle-retention.md#orphaned-storage-cleanup).

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
