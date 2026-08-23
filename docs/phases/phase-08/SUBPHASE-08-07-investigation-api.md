# Sub-phase 07 — Investigation API

**Prerequisites:** [Sub-phase 04](./SUBPHASE-08-04-abuse-admin-api.md) — open abuse report on listing

## Summary
Admin-only access to private investigation data **only when** an open `AbuseReport` exists for that listing.

## Endpoints
| Route | Returns |
| ----- | ------- |
| `GET /api/admin/investigations/{reportId}/chat` | Chat threads + messages for report's claims |
| `GET /api/admin/investigations/{reportId}/claims` | Claim text + claim photo URLs |
| `GET /api/admin/investigations/{reportId}/photos` | Private report photo pre-signed URLs (gated — not unconditional admin photo access from Phase 02) |

## Rules
- No open abuse flag → `404` (caller lacks visibility per error contract)
- Hidden verification detail → **still never** returned
- Completes Phase 05 admin claim photo access stub

## Exit criteria
- [ ] Investigation access gated on open flag; hidden detail absent
- [ ] Mark complete in README
