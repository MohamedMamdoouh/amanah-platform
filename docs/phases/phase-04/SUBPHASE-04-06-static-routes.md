# Sub-phase 06 — Static Routes & Error Pages

**Status:** Not started  
**Prerequisites:** Phase 01 Angular shell ([Sub-phase 10](../phase-01/SUBPHASE-01-10-angular-shell.md))

---

## 1. Summary

Add `/not-found` and `/unavailable` static pages and wire Angular router error handling stubs. No browse API dependency — can be built in parallel with API sub-phases 01–05.

---

## 2. SPEC references

| SPEC section | Topic |
| ------------ | ----- |
| Section 4.4 | Not-found and permanently-unavailable pages |
| Section 15.3 | URL behavior |

---

## 3. What you will learn

- Angular wildcard route vs explicit error routes
- Arabic RTL copy for error states
- Navigation back to `/browse` from error pages
- Preparing detail page (sub-phase 08) to navigate here on API error codes

**Files to read after implementing:**

- `web/src/app/features/errors/not-found-page/`
- `web/src/app/features/errors/unavailable-page/`
- `web/src/app/app.routes.ts`

---

## 4. Deliverables

### Pages

| Route | Purpose |
| ----- | ------- |
| `/not-found` | Generic not-found (missing report, wrong type, pending to public) |
| `/unavailable` | Report was resolved, withdrawn, or removed |

### Copy (Arabic examples)

**Not found:**
- Heading: "البلاغ غير موجود"
- Body: "لم نتمكن من العثور على هذا البلاغ."
- CTA: "العودة إلى التصفح" → `/browse`

**Unavailable:**
- Heading: "هذا البلاغ لم يعد متاحاً"
- Body: "تم إغلاق أو سحب هذا البلاغ."
- CTA: "تصفح البلاغات الأخرى" → `/browse`

### Router config

```typescript
{ path: 'not-found', component: NotFoundPageComponent },
{ path: 'unavailable', component: UnavailablePageComponent },
// Wildcard last:
{ path: '**', redirectTo: 'not-found' }
```

### Shared error layout

- Reuse app shell (header/footer from Phase 01)
- RTL-friendly centered content
- No API calls on these pages

### Helper service stub

`ErrorNavigationService` (optional):

| Method | When |
| ------ | ---- |
| `navigateNotFound()` | API `report.not_found` |
| `navigateUnavailable()` | API `report.unavailable` |

Used in sub-phase 08.

---

## 5. Step-by-step implementation order

1. Generate `NotFoundPageComponent` and `UnavailablePageComponent`
2. Add Arabic copy and browse link button
3. Register routes in `app.routes.ts`
4. Add wildcard `**` redirect (ensure it doesn't break admin routes — use route order carefully)
5. Manual navigate to `/not-found` and `/unavailable` in browser
6. Add `ErrorNavigationService` if helpful

---

## 6. Out of scope

- Detail page API error handling (sub-phase 08)
- Browse page (sub-phase 07)
- Custom 404 for admin section (admin has own layout)

---

## 7. Validation gate

### Automated tests

- [ ] Router navigates to `/not-found` component
- [ ] Router navigates to `/unavailable` component
- [ ] Browse CTA link has correct `routerLink`

### Manual smoke checklist

- [ ] Visit `/not-found` — page renders RTL Arabic
- [ ] Visit `/random-url` — lands on not-found (wildcard)
- [ ] Admin routes still work (`/admin/...`)

---

## 8. Exit criteria

- [ ] Error pages render correctly
- [ ] Mark sub-phase 06 complete in [phase-04/README.md](./README.md)
