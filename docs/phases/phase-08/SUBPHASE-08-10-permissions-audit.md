# Sub-phase 10 — Permissions Matrix Audit

**Prerequisites:** All feature sub-phases 01–09

## Summary
Automated regression suite for Section 9 — complements incremental enforcement in Phases 01–07 (see `docs/phases/README.md` cross-phase rule 1).

## Deliverables
`PermissionsMatrixTests` with cases for:
- Public vs logged-in vs reporter vs claimant vs admin
- Each data type: title, category fields, reward/held location, photos (public/private), hidden detail, claim text/photo, chat, phone, withdrawal reason, **display name of claimant**
- Private report photos: admin access only during moderation review **or** open abuse investigation (not unconditional)
- Assert hidden detail never in admin responses
- Assert phone never to other users

## Exit criteria
- [ ] Matrix tests pass in CI
- [ ] Mark complete in README
