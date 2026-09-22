# Specifications

Implementation specs for Amanah, in recommended reading order. Authoritative **build status** lives here; the root [README.md](../README.md) summarizes for onboarding. Phase 00 (platform foundation) is documented in the README and [SPEC.md](./SPEC.md) Part II — no separate phase spec.

| # | Document | Topic | Status |
| - | -------- | ----- | ------ |
| — | [SPEC.md](./SPEC.md) | Master product & technical specification | v9 |
| 00 | [README](../README.md) · [SPEC.md](./SPEC.md) Part II | Platform foundation (auth, deploy, seeds) | Complete |
| — | [00-api-conventions.md](./00-api-conventions.md) | API error envelope, status codes, versioning | Reference |
| 01 | [01-report-submission.md](./01-report-submission.md) | Lost/found report creation | Complete |
| 02 | [02-admin-moderation.md](./02-admin-moderation.md) | Moderation queue, resubmit, categories, notifications | Complete (manual smoke pending) |
| 03 | [03-browse-discovery.md](./03-browse-discovery.md) | Browse, search, filters | Complete |
| 04 | [04-claims-verification.md](./04-claims-verification.md) | Claims and ownership verification | Complete (manual smoke pending) |
| 05 | [05-chat-resolution-notifications.md](./05-chat-resolution-notifications.md) | Chat, resolution (+ remaining notification events) | Complete (manual smoke pending) |
| 06 | [06-lifecycle-retention.md](./06-lifecycle-retention.md) | Expiry, retention, account deletion | Complete (manual smoke pending) |
| 07 | [07-trust-safety-launch.md](./07-trust-safety-launch.md) | Trust, safety, launch readiness | In progress (API + partial admin UI) |

When merging a phase, update this table and the root [README.md](../README.md) in the same PR as the feature work.

**Operational docs:** [docs/deployment.md](../docs/deployment.md), [docs/observability.md](../docs/observability.md)
