# Sub-phase 02 — Flag Listing API

**Prerequisites:** [Sub-phase 01](./SUBPHASE-08-01-decisions.md), Phase 04 public reports

## Summary
`POST /api/reports/{id}/flag` and `GET /api/reports/{id}/flag` for open flag lookup.

## Rules (SPEC 15.6 + 7.1)
- Logged-in non-owner only
- Report must be `Published` or `Claim In Progress`
- One open flag per user per listing
- Predefined reason required; optional note
- **`POST` duplicate:** if user already has an open flag on this listing → `409 abuse.duplicate_flag`
- **`GET`:** returns user's open flag if any, else `404`
- Flagging does **not** change report status or public visibility (SPEC 7.1 — listing stays visible until admin acts)

## Deliverables
- Migration: ensure `AbuseReport` entity exists; reuse Phase 01 `User.IsBanned`, `User.BanReason`; add `BannedAt` in [Sub-phase 01](./SUBPHASE-08-01-decisions.md) if missing
- `POST` creates `AbuseReport` row: `Open`
- `GET` returns user's open flag or `404`

## Exit criteria
- [ ] All flag constraint tests pass; mark complete in README
