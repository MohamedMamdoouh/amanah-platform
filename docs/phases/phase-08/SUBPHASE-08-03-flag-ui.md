# Sub-phase 03 — Flag Listing UI

**Prerequisites:** [Sub-phase 02](./SUBPHASE-08-02-flag-api.md)

## Summary
Flag button on `/lost/{id}` and `/found/{id}` (logged-in, non-owner). Report shortcut in chat header on `/my/chats/{threadId}`.

## Deliverables
- Flag dialog: reason dropdown + optional note
- If `GET flag` returns open flag → show existing flag dialog (no `POST`)
- If no open flag → reason dropdown + optional note → `POST`
- Chat header: same flow for linked report
- Hide for owner and logged-out users

## Exit criteria
- [ ] Flag from browse and chat E2E; mark complete in README
