# Sub-phase 01 — Reference Data APIs

**Status:** Not started  
**Prerequisites:** Phase 01 complete ([Sub-phase 09 — Schema & Seeds](../phase-01/SUBPHASE-01-09-schema-seeds.md))

---

## 1. Summary

Expose read-only reference endpoints for categories (with field definitions) and governorates. These power submission forms in later sub-phases. No report writes, no auth required — public catalog data from Phase 01 seeds.

---

## 2. SPEC references

| SPEC section | Topic                                                  |
| ------------ | ------------------------------------------------------ |
| Section 5.2  | Category list, field definitions, `photosPrivate` flag |
| Section 5.3  | 27 Egyptian governorates                               |

---

## 3. What you will learn

- EF Core queries with `.Include()` for category field definitions
- Filtering active categories only (`active = true`)
- DTO projection — what to expose vs keep internal
- Why `photosPrivate` is returned to the client (form needs to know upload routing in sub-phase 04)

**Files to read after implementing:**

- `api/Controllers/CategoriesController.cs`
- `api/Controllers/GovernoratesController.cs`
- `api/Dtos/Reports/` — category and governorate response DTOs
- `api.Tests/Controllers/CategoriesControllerTests.cs`
- `api.Tests/Controllers/GovernoratesControllerTests.cs`

---

## 4. Deliverables

### Endpoints

| Method | Route               | Auth | Success response                   |
| ------ | ------------------- | ---- | ---------------------------------- |
| GET    | `/api/categories`   | None | `200` — array of active categories |
| GET    | `/api/governorates` | None | `200` — array of governorates      |

### `GET /api/categories` response shape

Each category:

| Field           | Type    | Notes                                |
| --------------- | ------- | ------------------------------------ |
| `id`            | UUID    |                                      |
| `name`          | string  | Arabic display name                  |
| `slug`          | string  | e.g. `documents-ids`                 |
| `photosPrivate` | boolean | Drives upload bucket in sub-phase 04 |
| `fields`        | array   | Sorted by `sortOrder`                |

Each field definition:

| Field       | Type                    | Notes                                        |
| ----------- | ----------------------- | -------------------------------------------- |
| `key`       | string                  | e.g. `brand-model`, `key-count`              |
| `label`     | string                  | Arabic label                                 |
| `type`      | `"text"` \| `"integer"` |                                              |
| `required`  | boolean                 |                                              |
| `minLength` | int?                    | For text fields                              |
| `maxLength` | int?                    | For text fields                              |
| `minValue`  | int?                    | For integer fields                           |
| `maxValue`  | int?                    | For integer fields                           |
| `pattern`   | string?                 | e.g. letters-and-spaces for first-name field |
| `helpText`  | string?                 | Optional Arabic helper below label (e.g. first-name: no surnames) |

Map validation rules from seed `CategoryFieldDefinition` rows — do not hardcode in the controller.

### `GET /api/governorates` response shape

| Field       | Type            |
| ----------- | --------------- |
| `id`        | UUID            |
| `name`      | string (Arabic) |
| `sortOrder` | int             |

Sorted by `sortOrder` ascending.

### Query rules

- Categories: `WHERE active = true`, include field definitions
- Deactivated categories: excluded entirely
- Governorates: all 27 seeded rows, no filter

---

## 5. Step-by-step implementation order

1. Create response DTOs under `api/Dtos/Reports/`
2. Add `CategoriesController` with single `GetAll` action
3. Add `GovernoratesController` with single `GetAll` action
4. Map entities → DTOs (manual or AutoMapper — match Phase 01 style)
5. Write integration tests against seeded data
6. Manual `curl` or Swagger check — verify 8 categories, Documents/IDs has `photosPrivate: true`

---

## 6. Out of scope

- Admin category management (Phase 03)
- Report submission (sub-phases 05+)
- Caching headers (optional; skip in v1)
- Angular UI (sub-phase 07)

---

## 7. Validation gate

### Automated tests

- [ ] `GET /api/categories` returns 8 active categories
- [ ] Documents/IDs category has `photosPrivate: true` and 2 required fields
- [ ] Keys category includes `key-count` integer field with min 1, max 20
- [ ] Deactivated category (if you add one in test seed) is excluded
- [ ] Field definitions sorted by `sortOrder`
- [ ] `GET /api/governorates` returns 27 entries sorted by `sortOrder`
- [ ] Both endpoints return `200` without auth header

### Manual smoke checklist

- [ ] Hit both endpoints in Swagger or browser — inspect JSON structure
- [ ] Confirm Arabic names render correctly (UTF-8)

---

## 8. Exit criteria

- [ ] All automated tests pass
- [ ] Response shapes stable enough for Angular services in sub-phase 07
- [ ] Mark sub-phase 01 complete in [phase-02/README.md](./README.md)
