# Sub-phase 08 — Public Detail Pages

**Status:** Not started  
**Prerequisites:** [Sub-phase 05 — Public Detail API](./SUBPHASE-04-05-public-detail-api.md), [Sub-phase 06 — Static Routes](./SUBPHASE-04-06-static-routes.md), [Sub-phase 07 — Browse UI](./SUBPHASE-04-07-browse-ui.md)

---

## 1. Summary

Build `/lost/{id}` and `/found/{id}` public detail pages with status-aware routing to `/not-found` and `/unavailable`. Show full public report content, `Claim In Progress` label, claim/message CTA stubs (login prompt when logged out), and My Reports Published-tab integration (SPEC 4.8).

---

## 2. SPEC references

| SPEC section | Topic |
| ------------ | ----- |
| Section 4.4 | Public detail; claim in progress label |
| Section 4.8 | My Reports Published tab → public URL |
| Section 3 | Login required for actions — prompt on tap |
| Section 6.5 | Claim unavailable while Claim In Progress |
| Section 15.3 | Full browse/discovery acceptance criteria |

---

## 3. What you will learn

- Route param `id` → API fetch by type (send auth header when logged in for reporter/admin carve-out)
- HTTP error code → navigate to error pages
- Photo gallery for public photos only
- Claim CTA: hide for reporter on own report; Phase 05 rejects self-claims server-side as backstop
- Message CTA stub: login prompt only until Phase 06 chat

**Files to read after implementing:**

- `web/src/app/features/report-detail/lost-detail-page/`
- `web/src/app/features/report-detail/found-detail-page/`
- `web/src/app/features/report-detail/public-report.service.ts`
- `web/src/app/features/report-detail/report-detail-layout/` (shared)

---

## 4. Deliverables

### Routes

| Route | Component | API |
| ----- | --------- | --- |
| `/lost/:id` | `LostDetailPageComponent` | `GET /api/lost/{id}` (+ auth header if logged in) |
| `/found/:id` | `FoundDetailPageComponent` | `GET /api/found/{id}` (+ auth header if logged in) |

### Shared detail layout

- Title, type badge (مفقود / موجود)
- **Claim in progress banner** when `claimInProgress`
- Description, category fields, location, date
- Found: held location section
- Reward section if offered
- Photo gallery (lightbox)
- Reporter display name (no phone)
- Breadcrumb: تصفح → {title}

### Claim CTA button

| State | Behavior |
| ----- | -------- |
| Logged out | Tap → redirect to login with `returnUrl` |
| Logged in + `Published` + not own report | Button visible but **disabled** ("تقديم المطالبة — قريباً") until Phase 05 |
| `Claim In Progress` | Button hidden or disabled with explanation |
| Reporter viewing own report | **Hide** claim button |

Detect own report: authenticated API returns reporter detail for pending/rejected; for published reports use `GET /api/reports/mine` check or `isOwnReport` when API provides it.

### Message CTA stub (SPEC 15.3 phase gate)

| State | Behavior |
| ----- | -------- |
| Logged out | Tap → login with `returnUrl` (same as claim) |
| Logged in | Hidden or disabled until Phase 06 — no chat navigation yet |

### My Reports Published tab integration (SPEC 4.8)

- Verify Phase 03 [sub-phase 10](../phase-03/SUBPHASE-03-10-notifications-resubmit-ui.md) Published tab links to `/lost/{id}` or `/found/{id}` by report type
- Smoke: My Reports → Published → opens this detail page successfully

### Error handling

| API error | Navigate |
| --------- | -------- |
| `report.not_found` | `/not-found` |
| `report.unavailable` | `/unavailable` |
| Network error | Inline retry message |

### `PublicReportService`

| Method | API |
| ------ | --- |
| `getLost(id)` | `GET /api/lost/{id}` with optional auth |
| `getFound(id)` | `GET /api/found/{id}` with optional auth |

---

## 5. Step-by-step implementation order

1. Create `PublicReportService` (optional auth header)
2. Build shared `ReportDetailLayoutComponent`
3. Build `LostDetailPageComponent` — fetch on init
4. Build `FoundDetailPageComponent`
5. Wire error navigation to static pages
6. Add claim in progress banner
7. Add claim + message CTA stubs with login redirect when logged out
8. Hide claim CTA for reporter's own published report
9. Link report cards from browse (sub-phase 07)
10. Verify My Reports Published tab deep links
11. Run full Phase 04 manual smoke checklist

---

## 6. Out of scope

- Claim submission form (Phase 05)
- Abuse flag button (Phase 08)
- Functional chat / messaging (Phase 06)
- Social share / OG tags

---

## 7. Validation gate

### Automated tests

- [ ] Lost page calls correct API endpoint with auth when logged in
- [ ] `report.not_found` navigates to `/not-found`
- [ ] `report.unavailable` navigates to `/unavailable`
- [ ] Claim in progress banner renders when flag true
- [ ] Logged-out claim tap redirects to login
- [ ] Logged-out message tap redirects to login
- [ ] Reporter own published report — claim button hidden

### Manual smoke checklist (Phase 04 exit)

- [ ] Open published `/lost/{id}` logged-out — full detail, photos visible
- [ ] Open `/found/{id}` for found report — held location shown
- [ ] Wrong type URL → not-found page
- [ ] Pending report URL (as stranger) → not-found
- [ ] Pending report URL (as reporter) → detail visible
- [ ] Resolved report → unavailable page
- [ ] Claim In Progress report — banner visible; claim disabled
- [ ] Logged-out tap claim → login prompt
- [ ] Logged-out tap message → login prompt
- [ ] Browse → card click → detail works
- [ ] My Reports Published tab → public detail works
- [ ] Private category published report — no photos on public detail

### Phase 04 final gate

Re-run all items from [PHASE-04-browse-discovery.md](./PHASE-04-browse-discovery.md#9-definition-of-done).

---

## 8. Exit criteria

- [ ] All Phase 04 acceptance criteria pass
- [ ] Mark sub-phase 08 and Phase 04 complete in [phase-04/README.md](./README.md)
- [ ] Ready to start Phase 05 — Claims & Verification
