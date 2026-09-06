# Amanah — UI/UX & Design System Context

Context for designing the Amanah interface and design system. Non-technical; focused on product meaning, tone, screens, and UX patterns.

---

## What it is

**Amanah** is an Egyptian lost-and-found platform.

It connects people who lost something with people who found it — with moderation, ownership verification, and in-app messaging. Phone numbers are never shown between users.

### Problem it solves

In Egypt, lost/found posts usually happen on Facebook groups. That is messy, unmoderated, hard to search, risky (scams, no proof of ownership), and forces people to share phone numbers immediately.

### Vision

A real product for real Egyptians — not a demo. Success is measured by **confirmed reunions**: when an item is actually returned and both parties agree it happened.

### Audience

- Nationwide (all of Egypt)
- **Arabic-only UI**, RTL layout
- Anyone can browse; login required to post, claim, chat, or confirm returns
- Everyday items: phones, IDs, wallets, keys, bags, etc.

---

## Brand & tone

- **Trustworthy, calm, safety-first** — not social-media chaos
- Practical and respectful; avoid hype or gamification
- Privacy matters: no phone exposure, verification before handover
- Safety is visible: meet in public, verify before giving items back
- Egyptian context — familiar, local, dignified

### Core trust pillars (repeat across UI)

1. **Review before publish** — every report is moderated before it goes public
2. **Verify before handover** — claims are reviewed by the original reporter
3. **Chat inside the platform** — phone numbers are never shared with other users

---

## Platform

- **Web application only** — runs in mobile and desktop browsers
- **Responsive design** — must work well on phone browsers (primary use case)
- **No native mobile app** (iOS/Android) in v1
- **No installable PWA** — users open it in the browser like a normal website

---

## Localization

- Default language: Arabic (`ar`), RTL in `index.html`
- Translation files: `web/src/assets/i18n/ar/` — `common`, `categories`, `governorates`, `errors`, `rejection-reasons`, `admin-moderation`, `admin-categories`, `notifications`, `pages`, `reports`
- Catalog labels (categories, governorates) use English keys in the API; Arabic display strings live in i18n JSON

---

## Global layout

Every page shares a consistent shell:

- **App shell:** header (logo, nav, login/logout, notifications badge) + footer (Terms, Privacy, Safety, Support)
- **Admin shell:** separate admin header + navigation for moderation and categories
- **Mobile-first** — design for small screens first, scale up for desktop

---

## Pages — Built today

| Page                                  | Purpose                                                                                     |
| ------------------------------------- | ------------------------------------------------------------------------------------------- |
| **Home / Landing**                    | Hero, trust pillars, CTAs (report lost / report found)                                      |
| **Auth (`/login`)**                   | Single page with tabs: sign-in, sign-up (phone → OTP → profile), forgot-password            |
| **Report lost / found**               | Full form: category, details, location, photos, hidden verification detail, optional reward |
| **Report form — confirmation**        | Inline state on the same route after submit (“under review, usually within a day”)          |
| **My Reports**                        | Tabs: Pending review, Rejected, Published (more tabs planned)                               |
| **My Report detail**                  | View report; withdraw while pending; rejection reason + resubmit when rejected              |
| **Notifications**                     | In-app notification center (moderation events today)                                        |
| **Terms / Privacy / Safety / Support**| Legal and support static pages                                                              |
| **Admin — Moderation queue**          | Pending reports, search, FIFO list                                                          |
| **Admin — Moderation review**         | Approve / reject with reason + note                                                         |
| **Admin — Categories**                | Category and field management (`/admin/categories`)                                           |

**Nav note:** Browse appears as a **disabled** header link until Phase 03 (no `/browse` route yet).

---

## Pages — Planned (full v1)

### Public discovery

| Page                                                   | Purpose                                                                                     |
| ------------------------------------------------------ | ------------------------------------------------------------------------------------------- |
| **Browse / Search listings**                           | Keyword search + filters (category, governorate, lost/found, date range); paginated results |
| **Public report detail** (`/lost/{id}`, `/found/{id}`) | Full listing for published reports; “claim in progress” badge when applicable               |
| **404 — Not found**                                    | Invalid ID or private report (pending/rejected)                                             |
| **Permanently unavailable**                            | Resolved, withdrawn, or removed reports                                                     |

### Claims & reunion

| Page / surface                            | Purpose                                                                              |
| ----------------------------------------- | ------------------------------------------------------------------------------------ |
| **Submit claim** (on public detail)       | Describe item + optional photo; contact info blocked in claim text                   |
| **Claims section** (on reporter’s detail) | Review pending claims; approve / reject                                              |
| **Chat thread**                           | Real-time messaging after claim approved; safety banner on new threads; text + photo |
| **My Claims**                             | All claims the user submitted, with status                                           |
| **My Chats**                              | All chat threads (active or read-only)                                               |
| **Confirm resolution**                    | Both parties confirm the item was returned                                           |
| **Withdraw published report**             | Reporter withdraws `Published` report with optional reason (Phase 06)                |
| **Flag / report listing**                 | Abuse reporting from browse or chat                                                  |
| **Login prompt**                          | Modal or redirect when a logged-out user tries a protected action                    |

### Account

| Page / surface                   | Purpose                                                |
| -------------------------------- | ------------------------------------------------------ |
| **Account / settings** (minimal) | Delete account; show display name (not editable in v1) |
| **Banned state**                 | Clear message when a banned user tries to sign in      |

### My Reports — additional tabs

- Claim in progress
- Closed (withdrawn, resolved, removed by admin)

### Admin — additional pages

| Page                    | Purpose                                     |
| ----------------------- | ------------------------------------------- |
| **Abuse report queue**  | Review flagged listings                     |
| **Abuse report detail** | Resolve: no action / takedown / ban user    |
| **User lookup**         | Find user, view activity, ban / unban       |

---

## Key UI patterns to design for

- **Status badges** — report and claim lifecycle (pending review, published, claim in progress, rejected, resolved, withdrawn, etc.)
- **Empty states** — no reports, no claims, no chats, no notifications, no search results
- **Loading states** — lists, forms, photo upload, chat
- **Form validation** — field errors, contact-info blocking, quota/cap messaging
- **Confirmation dialogs** — approve/reject claim, withdraw report, confirm resolution, delete account
- **Rejection / reason pickers** — admin reject reasons, withdraw reasons, flag reasons
- **Photo upload** — report photos (0–5), claim photo (1), chat attachments
- **Notification items** — typed events with links to the relevant page or section
- **Safety banner** — persistent in new chat threads
- **Pagination** — browse results (20 per page)
- **Tabs** — My Reports, My Claims (likely), auth modes
- **Cards** — report list items, home principles, browse results
- **Alerts** — errors, success feedback, quota warnings

---

## Out of scope for v1

- Native mobile apps (iOS/Android)
- Installable PWA / offline mode
- Maps or GPS location picker
- In-app payments or escrow for rewards
- English UI
- Notification preferences (all notifications always on)
- Block user (flag listing instead)
- Social share cards / rich link previews
- Edit published reports (withdraw and submit a new report instead)
- Display name edits after signup
- Phone number change

---

## Design constraints

- **RTL Arabic** throughout
- **WCAG 2.1 AA** as a goal — keyboard navigation and screen reader support on core flows (browse, submit, claim, chat)
- **Gregorian dates**, Western numerals (123), Africa/Cairo timezone
- **Browser support:** last 2 major versions of Chrome, Safari, Firefox, Edge; Android WebView 90+; iOS Safari 15+
- **Photos:** thumbnails optimized for mobile networks
