# Sub-phase 01 — Enforcement Decisions

**Prerequisites:** Phase 07 complete

## Summary
Document domain choice and define flag/abuse domain enums before implementation.

## Deliverables
### Flag reasons (SPEC 7.1 — 5 predefined)
Document exact enum values and Arabic labels from SPEC.

### `AbuseResolutionOutcome`
`NoAction`, `ReportTakedown`, `UserBanned`

### `AbuseReportStatus`
`Open`, `Resolved`

### Domain decision
Record chosen domain in `docs/LAUNCH.md` (or repo `README`) with Railway custom-domain steps — exit criterion requires an actual hostname (not "documented" without a value).

### Database migration
- `AbuseReport` table per SPEC Section 17
- **Reuse** Phase 01 `User` ban fields (`IsBanned`, `BanReason` from [sub-phase 04](../phase-01/SUBPHASE-01-04-auth-db.md)); add `BannedAt` column in this migration if not present — do not rename to conflicting property names

## Exit criteria
- [ ] Enums committed; domain hostname recorded in `docs/LAUNCH.md`
- [ ] Mark complete in README
