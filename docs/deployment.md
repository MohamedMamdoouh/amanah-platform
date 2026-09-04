# Deployment

Production runs on a **$0/month MVP stack** (pre-launch). SMS is pay-as-you-go on top.

| Component              | Provider      | Role                        |
| ---------------------- | ------------- | --------------------------- |
| Angular SPA + .NET API | Render        | Single web service (Docker) |
| PostgreSQL             | Supabase      | Primary database            |
| Media storage          | Cloudflare R2 | Report photos               |
| SMS (OTP)              | Unimtx        | Phone verification          |

One public origin serves both the app and `/api/v1/*`.

---

## Setup order

1. **Supabase** — create project and database
2. **Render** — deploy the Docker web service; configure secrets and connection string
3. **Cloudflare R2** — create bucket and credentials for media
4. **Unimtx** — create account, add credit, configure SMS API key
5. **Keepalive** (optional) — scheduled ping to avoid free-tier spin-down

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
