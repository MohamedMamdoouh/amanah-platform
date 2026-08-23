# Sub-phase 10 — Angular Shell & Static Pages

**Status:** Not started  
**Prerequisites:** None required for static shell work (may run in parallel with sub-phases 08–09 once Angular scaffold from sub-phase 02 exists)

---

## 1. Summary

Build the Angular UI foundation: RTL app shell with footer, static legal/support pages, PWA manifest and service worker shell, and an HTTP interceptor skeleton. No login API wiring yet.

---

## 2. SPEC references

| SPEC section | Topic |
| ------------ | ----- |
| Section 5.8 | Terms of Service and Privacy Policy pages |
| Section 11.1 | Browser support |
| Section 11.2 | RTL, accessibility baseline, Cairo time display prep |
| Section 16 | Angular PWA architecture |

---

## 3. What you will learn

- Angular routing with lazy-loaded feature modules or standalone route components
- RTL layout with `dir="rtl"` and CSS logical properties
- PWA configuration: `manifest.webmanifest`, `@angular/service-worker`, `ngsw-config.json`
- Static content components for legal pages
- HTTP interceptor pattern (attach JWT in sub-phase 11)

**Files to read after implementing:**

- `web/src/app/app.config.ts` — providers and interceptors
- `web/src/app/layout/` — shell, header, footer components
- `web/src/app/pages/` — static page components
- `web/ngsw-config.json`
- `web/public/manifest.webmanifest`

---

## 4. Deliverables

### App shell

| Component | Purpose |
| --------- | ------- |
| `AppShellComponent` | Root layout: header, `<router-outlet>`, footer |
| `HeaderComponent` | Logo/app name; login link placeholder (wired in sub-phase 11) |
| `FooterComponent` | Links to `/terms`, `/privacy`, `/safety`, `/support` |

### Routes

| Route | Component | Access |
| ----- | --------- | ------ |
| `/` | `HomeComponent` | Public — placeholder landing |
| `/terms` | `TermsComponent` | Public — static Arabic Terms of Service |
| `/privacy` | `PrivacyComponent` | Public — static Arabic Privacy Policy |
| `/safety` | `SafetyComponent` | Public — safety guidance for users |
| `/support` | `SupportComponent` | Public — support email address |

### RTL and styling

- Root `index.html`: `lang="ar"` `dir="rtl"`
- Global styles: Arabic-friendly font stack (e.g. Noto Sans Arabic or system fallback)
- Use CSS logical properties (`margin-inline-start`, `padding-inline-end`) over left/right
- Western Arabic numerals (123) for any numbers shown (SPEC 11.2)

### PWA

| Item | Detail |
| ---- | ------ |
| Manifest | Arabic app name `أمانة`, `dir: rtl`, `lang: ar`, theme/background colors |
| Service worker | `ngsw-config.json` with app shell assets; `ng add @angular/pwa` |
| Installable | App passes basic PWA installability (manifest + SW registered) |

### HTTP interceptor skeleton

```typescript
// auth.interceptor.ts — skeleton only
export const authInterceptor: HttpInterceptorFn = (req, next) => {
  // TODO sub-phase 11: attach Authorization header from AuthService
  return next(req);
};
```

Register in `app.config.ts`; no token attachment yet.

### Static page content

Placeholder Arabic content is acceptable for sub-phase 10. Must include:

- **Terms:** minimum age 16, acceptance at signup reference
- **Privacy:** PDPL cross-border hosting disclosure **and** disclosure that correction/access/data-export rights are not self-serve in v1 (SPEC 5.8)
- **Safety:** guidance on safe handovers, reporting abuse
- **Support:** published support email address (use placeholder `support@amanah.example` until domain chosen in Phase 08)

---

## 5. Step-by-step implementation order

1. Create layout components (shell, header, footer)
2. Create static page components with Arabic placeholder content
3. Configure routes in `app.routes.ts`
4. Add global RTL styles and Arabic font
5. Run `ng add @angular/pwa`; configure manifest
6. Create auth interceptor skeleton
7. Verify all routes render correctly
8. Run Lighthouse PWA audit

---

## 6. Out of scope

- `/login` route and auth forms (sub-phase 11)
- `/admin` route and role guard (sub-phase 11)
- API calls to auth endpoints
- Turnstile widget (sub-phase 11)

---

## 7. Validation gate

### Automated checks

- [ ] `ng build` succeeds without errors
- [ ] `ng build --configuration=production` includes service worker

### Manual smoke checklist

- [ ] All routes (`/`, `/terms`, `/privacy`, `/safety`, `/support`) render with RTL layout
- [ ] Footer links navigate correctly when logged out
- [ ] Browser dev tools: `<html dir="rtl">` confirmed
- [ ] Lighthouse PWA: installable (manifest valid, SW registered)
- [ ] Keyboard navigation works on footer links (accessibility baseline)

---

## 8. Exit criteria

- [ ] All validation gate items pass
- [ ] Static pages reachable from footer on every route
- [ ] Mark sub-phase 10 complete in [phase-01/README.md](./README.md)
