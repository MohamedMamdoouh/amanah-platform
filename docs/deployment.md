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

Verify `/health`, the home page, and sign-in/sign-up after deploy.

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
