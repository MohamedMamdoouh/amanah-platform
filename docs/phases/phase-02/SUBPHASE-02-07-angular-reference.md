# Sub-phase 07 — Angular Reference & Upload Client

**Status:** Not started  
**Prerequisites:** API sub-phases 01–04 complete ([01](./SUBPHASE-02-01-reference-data.md)–[04](./SUBPHASE-02-04-photo-upload.md)), Phase 01 Angular shell ([Sub-phase 10](../phase-01/SUBPHASE-01-10-angular-shell.md))

---

## 1. Summary

Build Angular services and shared components that submission forms will use: category/governorate fetching, authenticated photo upload with progress, auth route guards, and reusable field renderers. No full submission forms yet — prepare the plumbing and verify with a minimal test page or unit tests.

---

## 2. SPEC references

| SPEC section | Topic |
| ------------ | ----- |
| Section 5.2 | Dynamic category fields |
| Section 5.3 | Governorate dropdown |
| Section 4.1 | Photo upload 0–5 |
| Section 11 | RTL form layout baseline |

---

## 3. What you will learn

- Angular `HttpClient` services with typed models matching API DTOs
- `authGuard` on `/report/*` and `/my/*` routes (redirect to login)
- Multipart upload with `FormData` and upload progress events
- Dynamic form control generation from category field definitions
- Handling private vs public photo preview URLs

**Files to read after implementing:**

- `web/src/app/core/services/categories.service.ts`
- `web/src/app/core/services/governorates.service.ts`
- `web/src/app/core/services/report-photo-upload.service.ts`
- `web/src/app/shared/components/category-field-input/` (dynamic field component)
- `web/src/app/core/guards/auth.guard.ts`
- `web/src/app/app.routes.ts` (route stubs with guards)

---

## 4. Deliverables

### Services

| Service | Methods |
| ------- | ------- |
| `CategoriesService` | `getCategories(): Observable<Category[]>` — cache in memory after first load |
| `GovernoratesService` | `getGovernorates(): Observable<Governorate[]>` |
| `ReportPhotoUploadService` | `upload(file, categoryId): Observable<UploadedPhoto>` with progress |

### Models

TypeScript interfaces mirroring API DTOs from sub-phase 01 (category, field definition, governorate, uploaded photo).

### Shared components

| Component | Purpose |
| --------- | ------- |
| `CategoryFieldInputComponent` | Renders text or integer input from field definition; shows `helpText` when present; emits value |
| `PhotoUploadSlotComponent` | Single photo picker with preview, remove, 5 MB client-side check |

### Routes (stubs)

Register guarded routes (components can be placeholders until sub-phases 08–10):

| Route | Guard | Component |
| ----- | ----- | --------- |
| `/report/lost` | `authGuard` | `LostReportPageComponent` (stub) |
| `/report/found` | `authGuard` | `FoundReportPageComponent` (stub) |
| `/my/reports` | `authGuard` | `MyReportsPageComponent` (stub) |

Logged-out user navigating to these → redirect to login with `returnUrl`.

### Error handling

Map API field errors and quota `429` responses using the error contract from Phase 01.

---

## 5. Step-by-step implementation order

1. Generate TypeScript models from API shapes (manual — keep in sync)
2. Implement `CategoriesService` and `GovernoratesService`
3. Implement `ReportPhotoUploadService` with JWT interceptor (from Phase 01)
4. Build `CategoryFieldInputComponent` — test with Storybook or shallow component test
5. Build `PhotoUploadSlotComponent`
6. Add `authGuard` and route stubs
7. Optional: minimal dev-only page at `/dev/report-widgets` to manually test upload — remove before sub-phase 10 or hide behind environment flag
8. Unit tests for services (HttpClientTestingModule)

---

## 6. Out of scope

- Full lost/found forms (sub-phases 08–09)
- My Reports list UI (sub-phase 10)
- Client-side contact-info detection (server is source of truth; optional UX hint OK but not required)
- Report create API call (sub-phase 08)

---

## 7. Validation gate

### Automated tests

- [ ] `CategoriesService` maps API response to typed models
- [ ] `ReportPhotoUploadService` sends multipart with `categoryId`
- [ ] `authGuard` redirects unauthenticated user to login
- [ ] `CategoryFieldInputComponent` validates integer range locally

### Manual smoke checklist

- [ ] Log in; navigate to `/report/lost` — stub page loads (not 404)
- [ ] Log out; navigate to `/report/lost` — redirected to login
- [ ] Dev widget: select category, upload image — thumbnail preview appears
- [ ] Upload to Documents/IDs — preview uses presigned URL flow

---

## 8. Exit criteria

- [ ] Services and shared components ready for forms
- [ ] Auth guards on all Phase 02 routes
- [ ] Mark sub-phase 07 complete in [phase-02/README.md](./README.md)
