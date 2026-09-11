# Phase 03 - Browse & Discovery

**Status:** Complete  
**Prerequisites:** Phase 02 - Admin Moderation

---

## 1. Summary

Enable anyone - including logged-out visitors - to browse, search, filter, and view public report detail pages. Only `Published` and `Claim In Progress` reports appear in discovery. Keyword search uses all-terms AND matching with Arabic normalization via a denormalized search column and `pg_trgm` index. Public URLs follow `/lost/{id}` and `/found/{id}` with status-appropriate not-found and permanently-unavailable pages.

---

## 2. SPEC references

| SPEC section | Topic                                                   |
| ------------ | ------------------------------------------------------- |
| Section 3    | Logged-out browsing parity, login required for actions  |
| Section 4.4  | Browsing and discovery                                  |
| Section 4.8  | My Reports Published tab integration                    |
| Section 8    | Public visibility by status                             |
| Section 9    | Public vs reporter vs admin visibility                  |
| Section 15.3 | Browse, search, visibility acceptance criteria          |
| Section 16   | Search implementation (`pg_trgm`, Arabic normalization) |

**Part II (technical):** Section 16 (search)

---

## 3. Prerequisites

### Prior phases

- [x] Platform foundation
- [x] Phase 01 - Report Submission (search column populated on write)
- [x] Phase 02 - Admin Moderation (`Published` reports exist)

### Deferred decisions (Section 14)

None additional.

---

## 4. Deliverables

### API

| Method | Route                         | Purpose                                               |
| ------ | ----------------------------- | ----------------------------------------------------- |
| GET    | `/api/v1/reports`             | Browse/search: `Published` + `Claim In Progress` only |
| GET    | `/api/v1/reports/{id}/public` | Public detail page data (status-aware)                |
| GET    | `/api/v1/lost/{id}`           | Alias redirect or shared handler for lost reports     |
| GET    | `/api/v1/found/{id}`          | Alias redirect or shared handler for found reports    |

Query parameters for browse: `q` (keyword), `category`, `governorate`, `type` (lost/found), `dateFrom`, `dateTo`, `page`, `pageSize` (default 20).

### UI routes

| Route          | Access | Purpose                                |
| -------------- | ------ | -------------------------------------- |
| `/browse`      | Public | Browse listing with search and filters |
| `/lost/{id}`   | Public | Lost report detail (status-aware)      |
| `/found/{id}`  | Public | Found report detail (status-aware)     |
| `/not-found`   | Public | Generic not-found page                 |
| `/unavailable` | Public | Permanently-unavailable page           |

### Database

- `pg_trgm` GIN index on `Report.normalizedSearchText`
- Enable `pg_trgm` extension in migration
- Verify search column backfill not needed (written since Phase 01)

### Infrastructure

- No browse caching in v1 — `GET /api/v1/reports` queries PostgreSQL on every request. Revisit caching if browse DB load becomes an issue.

### Shared utilities

- Arabic normalization for query: alef variants, `ى` -> `ي`, `ة` -> `ه`, strip tatweel/diacritics, collapse whitespace, lowercase
- All-terms AND matching via `ILIKE '%term%'` per normalized term
- Status-based response filtering: strip private photos, hidden detail, reporter phone
- `Claim In Progress` label on listing and detail; claim CTA prompts login when logged out, shows claim form on `Published` reports when logged in (Phase 04), disabled on `Claim In Progress`

---

## 5. Permissions (Section 9)

Server-enforce these matrix rows before marking this phase done:

| Data                                | Public visitor                    | Logged-in user | Reporter (own) | Admin        |
| ----------------------------------- | --------------------------------- | -------------- | -------------- | ------------ |
| Title, description, category fields | yes (Published/Claim In Progress) | yes            | yes            | yes          |
| Public photos                       | yes                               | yes            | yes            | yes          |
| Private photos                      | -                                 | -              | yes (own)      | yes (review) |
| Hidden verification detail          | -                                 | -              | yes (own)      | -            |
| Reward, held location               | yes                               | yes            | yes            | yes          |
| Display name of reporter            | yes                               | yes            | own            | yes          |
| Phone numbers                       | -                                 | own            | own            | yes          |

URL access by status (Section 4.4):

- `Pending Review`, `Rejected` -> not-found (except reporter/admin)
- `Resolved`, `Withdrawn`, `Removed by Admin` -> permanently-unavailable
- Missing IDs, wrong type -> not-found

---

## 6. Notifications (Section 5.7)

| Event | Recipient | Introduced                           |
| ----- | --------- | ------------------------------------ |
| -     | -         | No new notification types this phase |

---

## 7. Out of scope

Explicitly deferred to later phases:

- Chat and messaging -> Phase 05
- Abuse flagging UI -> Phase 07
- Social link previews -> out of scope v1 (Section 10)
- Map/GPS location -> out of scope v1 (Section 10)

---

## 8. Acceptance criteria

From [SPEC.md Section 15.3](./SPEC.md#153-browse-search-visibility-and-urls).

- [x] **Listing scope:** browse and filter requests return `Published` and `Claim In Progress` reports and nothing else
- [x] **Claim In Progress presentation:** a `Claim In Progress` report is publicly readable, labelled as having a claim in progress, and its claim action is unavailable
- [x] **Search behavior:** a query matches only reports where every query word appears, in any order and case-insensitively, across title, description, public category field values, and area text, after Arabic normalization - so a query using a bare alef, a haa in place of taa marbuta, tatweel, or no diacritics still matches the equivalent stored text
- [x] **Filters:** category, governorate, type, and date range combine with the keyword query using AND
- [x] **URL behavior:** `Resolved`, `Withdrawn`, and `Removed by Admin` show a permanently-unavailable page; `Pending Review` and `Rejected` show a not-found page to everyone but their reporter and the admin; missing IDs and wrong-type links show a not-found page

**Additional phase gate:**

- [x] Sort: newest published first
- [x] Pagination: 20 per page, numbered pages
- [x] Logged-out visitors see same content as logged-in on public reports
- [x] Claim action prompts login when logged out; claim form on `Published` reports when logged in (Phase 04)
- [x] Message action stub prompts login when logged out (full chat in Phase 05)

---

## 9. Definition of done

### Automated tests

- [x] Browse returns only `Published` and `Claim In Progress`
- [x] Arabic normalization search: alef variants, taa marbuta/haa, tatweel, diacritics
- [x] All-terms AND logic
- [x] Filters combine with keyword (AND)
- [x] Pagination and sort order
- [x] URL status routing (not-found, permanently-unavailable)
- [x] Private photos and hidden detail never in public API responses
- [x] Wrong-type URL (`/lost/{id}` for found report) -> not-found

### Manual smoke checklist

- [x] Browse as logged-out visitor; search and filter work
- [x] Open `/lost/{id}` and `/found/{id}` for published report
- [x] `Claim In Progress` report shows label; claim button disabled/prompts login
- [x] Resolved report URL shows permanently-unavailable page
- [x] Pending report URL shows not-found to other users

### Phase exit gate

This phase is complete when all acceptance criteria pass and no out-of-scope items were implemented early.
