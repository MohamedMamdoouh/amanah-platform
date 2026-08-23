# Sub-phase 08 — Enforcement Admin UI

**Prerequisites:** API sub-phases [04](./SUBPHASE-08-04-abuse-admin-api.md), [05](./SUBPHASE-08-05-takedown-api.md), [06](./SUBPHASE-08-06-ban-unban-api.md), [07](./SUBPHASE-08-07-investigation-api.md)

## Summary
`/admin/abuse` queue + detail with resolve actions. `/admin/users` search + ban/unban.

## Deliverables
### `/admin/abuse`
- FIFO list, pending count badge
- Detail: flag reason, listing summary, links to investigation viewers (chat, claims, private report photos)

### `/admin/users`
- Search by display name or phone
- User detail: `reportsCount` from `GET /api/admin/users/{id}`, ban status, ban/unban with reason

## Exit criteria
- [ ] Admin resolves abuse with each outcome manually
- [ ] Mark complete in README
