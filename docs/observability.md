# Observability

Amanah uses **Render-native, $0 observability**: structured JSON logs to stdout, correlation IDs, log-emitted metrics, split health endpoints, and GitHub Actions alerting.

---

## Log format

In **production**, the API emits **JSON logs** to stdout (Render log explorer). In development, logs remain human-readable.

Every request gets a correlation ID:

| Field | Source |
| ----- | ------ |
| `requestId` | `X-Request-Id` header (generated if missing) |
| `userId` | JWT subject claim (when authenticated) |

The response echoes `X-Request-Id` so clients and support can correlate failures.

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
- **Slow requests:** `event` = `http.request.completed` and `DurationMs` > 1000
- **Rate limits:** `event` = `metric` and `name` = `rate_limit.rejected`
- **Trace a request:** `requestId` = `<value from X-Request-Id>`
- **Report submissions:** `event` = `metric` and `name` = `report.submitted`

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
| `upload.photo.completed` | counter | Photo upload succeeded |
| `upload.photo.failed` | counter | Photo upload failed |
| `sms.send.completed` | counter | OTP SMS sent |
| `sms.send.failed` | counter | OTP SMS send failed |
| `otp.outbox.backlog` | gauge | Pending outbox messages each poll |

---

## Health endpoints

| Endpoint | Purpose |
| -------- | ------- |
| `GET /health` | **Liveness** — process is up (always 200) |
| `GET /health/ready` | **Readiness** — database + R2 (when configured) |

Readiness returns JSON:

```json
{ "status": "Healthy", "checks": { "database": "Healthy", "storage": "Healthy" } }
```

The keepalive workflow pings `/health`. Use `/health/ready` for deeper deploy verification.

---

## Alerting

[`.github/workflows/keepalive.yml`](../.github/workflows/keepalive.yml) runs every 10 minutes and pings `GET /health`. If the check fails, the workflow exits with an error.

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

To add a metrics backend later (e.g. OpenTelemetry + Grafana Cloud), instrument at the `AmanahMetrics` call sites or add an exporter wrapper — log-based metrics already define the names and tags to preserve.
