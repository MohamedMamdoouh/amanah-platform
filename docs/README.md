# Amanah docs

| Doc | Purpose |
| --- | ------- |
| [SPEC.md](./SPEC.md) | Product and technical specification |
| [api-error-contract.md](./api-error-contract.md) | API error envelope (`code`, `message`, `errors?`) |
| [PHASE-01-platform-foundation.md](./PHASE-01-platform-foundation.md) | Phase 01 tracker and deliverables |
| [PHASE-02-report-submission.md](./PHASE-02-report-submission.md) | Phase 02 |
| [PHASE-03-admin-moderation.md](./PHASE-03-admin-moderation.md) | Phase 03 |
| [PHASE-04-browse-discovery.md](./PHASE-04-browse-discovery.md) | Phase 04 |
| [PHASE-05-claims-verification.md](./PHASE-05-claims-verification.md) | Phase 05 |
| [PHASE-06-chat-resolution-notifications.md](./PHASE-06-chat-resolution-notifications.md) | Phase 06 |
| [PHASE-07-lifecycle-retention.md](./PHASE-07-lifecycle-retention.md) | Phase 07 |
| [PHASE-08-trust-safety-launch.md](./PHASE-08-trust-safety-launch.md) | Phase 08 |

## PostgreSQL

| Environment | How |
| ----------- | --- |
| Local dev | Native PostgreSQL 16+ on Windows (`amanah` on `localhost:5432`) |
| Integration tests | Testcontainers `postgres:16` (Docker); fixtures in `api.Tests/` |
| Production | Railway managed PostgreSQL |

EF Core uses `Npgsql` in all environments. No SQL Server.
