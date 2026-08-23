# Sub-phase 08 — Admin Category Management API

**Status:** Not started  
**Prerequisites:** Phase 02 [Sub-phase 01 — Reference Data](../phase-02/SUBPHASE-02-01-reference-data.md)

---

## 1. Summary

Implement admin CRUD for categories and field definitions: list (including inactive), create category, edit category, add/edit fields. No delete — deactivate only. Deactivated categories hidden from public `GET /api/categories` but existing reports keep their category.

---

## 2. SPEC references

| SPEC section | Topic |
| ------------ | ----- |
| Section 5.2 | Category management rules |
| Section 5.5 | Admin surfaces — `/admin/categories` |

---

## 3. What you will learn

- Admin vs public category endpoints — different visibility rules
- Slug uniqueness validation
- Field definition versioning — changes don't retroactively re-validate old reports
- Why categories with reports cannot be deleted

**Files to read after implementing:**

- `api/Controllers/Admin/CategoriesController.cs`
- `api/Services/Categories/AdminCategoryService.cs`
- `api/Dtos/Categories/AdminCategoryDto.cs`

---

## 4. Deliverables

### Endpoints

| Method | Route | Purpose |
| ------ | ----- | ------- |
| GET | `/api/admin/categories` | All categories incl. inactive + fields |
| POST | `/api/admin/categories` | Create category |
| PUT | `/api/admin/categories/{id}` | Edit category metadata |
| POST | `/api/admin/categories/{id}/fields` | Add field definition |
| PUT | `/api/admin/categories/{id}/fields/{fieldId}` | Edit field definition |

### `GET /api/admin/categories`

Returns all categories (active and inactive) with full field definitions — same shape as public categories plus `active` flag and internal IDs.

### `POST /api/admin/categories`

| Field | Rules |
| ----- | ----- |
| `name` | Required, Arabic, 2–80 chars |
| `slug` | Required, unique, lowercase kebab |
| `sortOrder` | int |
| `photosPrivate` | boolean |
| `active` | boolean, default true |

### `PUT /api/admin/categories/{id}`

Editable: `name`, `sortOrder`, `photosPrivate`, `active`

**Not editable:** `slug` (immutable after create — avoids breaking references)

**Deactivate:** `active = false` — hidden from new submissions; existing reports unchanged.

### `POST /api/admin/categories/{id}/fields`

| Field | Rules |
| ----- | ----- |
| `key` | Required, unique within category, kebab-case |
| `label` | Arabic |
| `type` | `text` or `integer` |
| `required` | boolean |
| `minLength`, `maxLength` | for text |
| `minValue`, `maxValue` | for integer |
| `pattern` | optional |
| `sortOrder` | int |

### `PUT /api/admin/categories/{id}/fields/{fieldId}`

Edit label, validation ranges, required flag, sort order.

**Not editable:** `key`, `type` (create new field instead — avoids data mismatch on old reports)

### Validation

- Duplicate slug → `409 category.slug_exists`
- Duplicate field key in category → `409`
- Category not found → `404`
- Non-admin → `403`

---

## 5. Step-by-step implementation order

1. Create admin DTOs (include `active`, entity IDs)
2. Implement `AdminCategoryService`
3. Add `CategoriesController` under `Admin/` route prefix
4. Integration tests: CRUD happy paths
5. Test deactivated category excluded from public `GET /api/categories`
6. Test existing report still references deactivated category

---

## 6. Out of scope

- Delete category or field (never in v1)
- Retroactive re-validation of old reports
- Angular admin UI (sub-phase 11)
- Changing `photosPrivate` on category with existing reports — allowed but does not move existing report photos (only resubmit re-derives per SPEC 4.3)

---

## 7. Validation gate

### Automated tests

- [ ] Admin lists inactive categories; public endpoint does not
- [ ] Create category → appears in admin list
- [ ] Deactivate category → hidden from `GET /api/categories`
- [ ] Add field definition → returned in category detail
- [ ] Duplicate slug → `409`
- [ ] Non-admin → `403`
- [ ] Edit field sort order persists

### Manual smoke checklist

- [ ] Create test category via Swagger; submit report with it (if active)
- [ ] Deactivate; confirm not in submission form category list

---

## 8. Exit criteria

- [ ] All category admin endpoints tested
- [ ] Public vs admin visibility rules correct
- [ ] Mark sub-phase 08 complete in [phase-03/README.md](./README.md)
