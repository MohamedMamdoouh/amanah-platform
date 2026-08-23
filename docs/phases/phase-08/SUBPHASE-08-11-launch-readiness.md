# Sub-phase 11 — Launch Readiness

**Prerequisites:** [Sub-phase 10](./SUBPHASE-08-10-permissions-audit.md)

## Summary
Final verification: rate limits, full Section 15 regression, Section 10 out-of-scope check, Section 13 risks, production config, custom domain.

## Rate limits (Section 7.5)
Verify each returns `429` + `Retry-After`:
- [ ] 3 reports/day
- [ ] 5 claims/day
- [ ] 5 open reports cap
- [ ] Photo uploads 5/min, 20/hour
- [ ] Chat 10/min, 60/hour
- [ ] OTP limits (Phase 01)

## Section 15 regression
Run acceptance checklists from **each phase doc** (Phases 01–08) covering Sections 15.1–15.9. Parent phase §8 lists Phase 08-specific items (15.6, 15.8); earlier sections regress via their originating phase docs.

## Section 10 out-of-scope (verify absent)
- [ ] Block user / per-user blocking
- [ ] Resolution disputes / reopen
- [ ] Claim rejection appeals
- [ ] Draft report saving
- [ ] Map/GPS location picker
- [ ] In-app payments/escrow
- [ ] Display-name / phone change after signup
- [ ] Data export / access requests
- [ ] Web push / SMS event notifications

## Section 13 risks (spot-check)
For each row in SPEC §13, confirm the listed mitigation exists in code or tests (document pass/fail in launch sign-off).

## Launch checklist
- [ ] Custom domain on Railway + HTTPS (`docs/LAUNCH.md` hostname matches production)
- [ ] Production SMS + email credentials
- [ ] Admin phone bootstrapped
- [ ] CORS, secrets, backups reviewed
- [ ] Privacy Policy: cross-border + v1 data-rights gaps (5.8)
- [ ] Listing copy / Terms mention 90-day auto-expiry (SPEC 4.7)
- [ ] Keyboard/RTL spot-check on browse, submit, claim, chat (Section 11.2)
- [ ] Browser matrix smoke: Chrome, Safari, Firefox, Edge (last 2 versions); mobile WebView 90+ / iOS Safari 15+ (Section 11.1)

## Exit criteria
- [ ] Launch checklist signed off
- [ ] Mark sub-phase 11 and **Phase 08 / v1** complete in README
