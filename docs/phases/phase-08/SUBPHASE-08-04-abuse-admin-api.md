# Sub-phase 04 — Abuse Admin API

**Prerequisites:** [Sub-phase 02](./SUBPHASE-08-02-flag-api.md)

## Summary
`GET /api/admin/abuse` (FIFO queue), `GET /api/admin/abuse/{id}`, `POST /api/admin/abuse/{id}/resolve`.

## Resolve outcomes
| Outcome | Action |
| ------- | ------ |
| `NoAction` | Close flag; notify flagger `AbuseReportResolvedForFlagger` via `INotificationService` (Phase 03) |
| `ReportTakedown` | Delegate to takedown service (sub-phase 05) |
| `UserBanned` | Delegate to ban service (sub-phase 06) |

Store resolution on `AbuseReport`; status → `Resolved`.

## Exit criteria
- [ ] Queue + resolve flows tested; mark complete in README
