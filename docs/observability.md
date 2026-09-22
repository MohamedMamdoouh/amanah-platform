# Observability

Amanah uses **Render-native, $0 observability**: structured JSON logs to stdout, correlation IDs, log-emitted metrics, split health endpoints, and GitHub Actions alerting.

---

## Log format

In **Production** (`ASPNETCORE_ENVIRONMENT=Production`), the API emits **JSON logs** to stdout (Render log explorer). In Development, logs remain human-readable. Staging does not switch on JSON logging.

Every request gets a correlation ID:

| Field | Source |
| ----- | ------ |
| `requestId` | `X-Request-Id` header (generated if missing; echoed in response) |
| `userId` | JWT subject on `http.request.completed` and `api.unhandled_error` when authenticated |

The Angular SPA does not yet send `X-Request-Id`; the server generates one per request.

---

## Event types

Filter Render logs by scope field `event`:

| `event` | Meaning |
| ------- | ------- |
| `http.request.completed` | Request finished (method, path, status, duration) |
| `metric` | Counter/histogram/gauge snapshot |
| `api.error` | Expected API error (validation, not found, etc.) |
| `api.unhandled_error` | Unexpected 500 |

Health probe traffic (`GET /health`, `GET /health/ready`) is omitted from request logs when successful.

---

## Common Render log queries

Examples (exact syntax may vary in Render UI):

- **Errors:** level `Error` or `event` = `api.unhandled_error`
- **Slow requests:** `event` = `http.request.completed` and duration > 1000 ms (verify field name in Render before relying on a filter)
- **Rate limits:** `event` = `metric` and `name` = `rate_limit.rejected`
- **Trace a request:** `requestId` = `<value from X-Request-Id>`
- **Report submissions:** `event` = `metric` and `name` = `report.submitted`
- **Lifecycle jobs:** level `Information` and message contains `Lifecycle job` or a job outcome field such as `WithdrawnCount`, `WarningsSent`, or `ExpiredCount`

Pair with Render service metrics (CPU, memory, HTTP latency) on the web service dashboard.

---

## Metrics catalog

Metrics are emitted as structured log lines (`event: metric`) in Render log explorer.

| Name | Type | When |
| ---- | ---- | ---- |
| `http.server.request.duration` | histogram (ms) | Every non-probe HTTP request |
| `http.server.request.errors` | counter | HTTP 5xx responses |
| `rate_limit.rejected` | counter | Rate limiter rejection |
| `report.submitted` | counter | Report created successfully |
| `sms.send.completed` | counter | OTP SMS accepted by Unimtx (`UnimtxSmsSender` only; Development `ConsoleSmsSender` does not emit this) |
| `sms.send.failed` | counter | OTP SMS send failed (Unimtx path) |
| `otp.outbox.backlog` | gauge | Pending OTP SMS outbox messages each poll |
| `email.admin_alert.outbox.backlog` | gauge | Pending admin alert email outbox messages each poll |
| `upload.photo.completed` | counter | Claim photo or chat attachment stored |
| `upload.photo.failed` | counter | Claim photo or chat attachment storage failed |

Report photos on `POST /api/v1/reports` do not emit `upload.photo.*`. `report.submitted` still records a successful report create.

---

## Health endpoints

| Endpoint | Purpose |
| -------- | ------- |
| `GET /health` | **Liveness** — process is up (always 200) |
| `GET /health/ready` | **Readiness** — database connectivity, plus R2 when `Bucket__*` is set. Returns **503** when unhealthy. An unset bucket is reported healthy (`In-memory storage`) so local dev can pass readiness; production must set the bucket variables. |

Readiness returns JSON:

```json
{ "status": "Healthy", "checks": { "database": "Healthy", "storage": "Healthy" } }
```

The keepalive workflow pings `/health`. Use `/health/ready` for deeper deploy verification.

---

## Alerting

[`.github/workflows/keepalive.yml`](../.github/workflows/keepalive.yml) runs every 10 minutes and pings `GET /health` on the `KEEPALIVE_URL` set in that workflow (`https://amanah-egh5.onrender.com`). If the check fails, the workflow exits with an error. Change `KEEPALIVE_URL` in the workflow when the public origin changes.

**Email alerts (GitHub):** no extra secrets or third-party services. Enable notifications so GitHub emails you when the workflow fails:

1. On the repo: **Watch** → **Custom** → check **Actions** (or **All activity**)
2. In GitHub account settings: **Notifications** → ensure **Actions** email is enabled

When keepalive fails, you’ll get a GitHub email with a link to the failed run. Check Render logs and `GET /health/ready` for readiness details.

**Test manually:** Actions → Keepalive → Run workflow (temporarily set a bad `KEEPALIVE_URL` in a branch to verify email, then revert).

---

## PII rules

**Never log:** phone numbers, OTP codes, passwords, JWTs, presigned URLs, report free-text content.

**Safe to log:** user ID (GUID), report ID, error codes, durations, storage keys (not URLs).

---

## Upgrade path

To add a metrics backend later (e.g. OpenTelemetry + Grafana Cloud), instrument at the `AppMetrics` call sites or add an exporter wrapper — log-based metrics already define the names and tags to preserve.
