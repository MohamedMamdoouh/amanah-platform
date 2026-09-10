# Specifications

Implementation specs for Amanah, in recommended reading order. Authoritative **build status** lives here; the root [README.md](../README.md) summarizes for onboarding. Platform foundation (auth, deploy, seeds) is documented in the README and [SPEC.md](./SPEC.md) Part II — no separate phase spec.

| # | Document | Topic | Status |
| - | -------- | ----- | ------ |
| — | [SPEC.md](./SPEC.md) | Master product & technical specification | v9 |
| 00 | [00-api-conventions.md](./00-api-conventions.md) | API error envelope, status codes, versioning | Reference |
| 01 | [01-report-submission.md](./01-report-submission.md) | Lost/found report creation | Complete |
| 02 | [02-admin-moderation.md](./02-admin-moderation.md) | Moderation queue, resubmit, categories, notifications | Complete (manual smoke pending) |
| 03 | [03-browse-discovery.md](./03-browse-discovery.md) | Browse, search, filters | Complete |
| 04 | [04-claims-verification.md](./04-claims-verification.md) | Claims and ownership verification | Not started |
| 05 | [05-chat-resolution-notifications.md](./05-chat-resolution-notifications.md) | Chat, resolution (+ remaining notification events) | Not started |
| 06 | [06-lifecycle-retention.md](./06-lifecycle-retention.md) | Expiry, retention, account deletion | Not started |
| 07 | [07-trust-safety-launch.md](./07-trust-safety-launch.md) | Trust, safety, launch readiness | Not started |

**UI/UX context:** [docs/ui-ux-context.md](../docs/ui-ux-context.md)  
**Operational docs:** [docs/deployment.md](../docs/deployment.md), [docs/observability.md](../docs/observability.md)
