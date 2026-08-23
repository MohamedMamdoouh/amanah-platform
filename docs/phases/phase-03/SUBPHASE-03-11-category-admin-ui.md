# Sub-phase 11 — Admin Category Management UI

**Status:** Not started  
**Prerequisites:** [Sub-phase 08 — Category Admin API](./SUBPHASE-03-08-category-admin-api.md), [Sub-phase 09](./SUBPHASE-03-09-admin-moderation-ui.md) (admin shell)

---

## 1. Summary

Build `/admin/categories` — list all categories (including inactive), create/edit categories, and manage field definitions. Final sub-phase of Phase 03. Run the full Phase 03 exit gate when done.

---

## 2. SPEC references

| SPEC section | Topic |
| ------------ | ----- |
| Section 5.2 | Category and field management |
| Section 5.5 | Admin surface at `/admin/categories` |

---

## 3. What you will learn

- Admin CRUD forms with validation
- Inline field-definition editor (add row, edit, reorder)
- Active/inactive toggle UX with confirmation
- `photosPrivate` flag implications — show warning when enabling

**Files to read after implementing:**

- `web/src/app/features/admin/categories/categories-page/`
- `web/src/app/features/admin/categories/category-form/`
- `web/src/app/features/admin/categories/field-definition-form/`
- `web/src/app/features/admin/categories/admin-categories.service.ts`

---

## 4. Deliverables

### Page: `/admin/categories`

**List view:**

- All categories sorted by `sortOrder`
- Columns: name, slug, active badge, `photosPrivate` badge, field count
- Actions: Edit, Deactivate/Activate
- "Add category" button

### Category create/edit form

| Field | Control |
| ----- | ------- |
| Name | Arabic text input |
| Slug | Text input (create only; read-only on edit) |
| Sort order | Number input |
| Photos private | Checkbox with warning copy |
| Active | Toggle |

### Field definitions section (on edit page)

- Table of fields: key, label, type, required, validation range
- Add field button → inline form or dialog
- Edit field — label, ranges, required, sort order (key/type read-only)
- Drag reorder optional — sort order number input is sufficient for v1

### `AdminCategoriesService`

| Method | API |
| ------ | --- |
| `getAll()` | `GET /api/admin/categories` |
| `create(dto)` | `POST /api/admin/categories` |
| `update(id, dto)` | `PUT /api/admin/categories/{id}` |
| `addField(categoryId, dto)` | `POST .../fields` |
| `updateField(categoryId, fieldId, dto)` | `PUT .../fields/{fieldId}` |

### Guards

- `adminGuard` on `/admin/categories`

---

## 5. Step-by-step implementation order

1. Implement `AdminCategoriesService`
2. Build category list page
3. Build category create form
4. Build category edit form with field definitions table
5. Build add/edit field dialog
6. Wire deactivate toggle with confirm dialog
7. Verify new active category appears in public submission forms
8. Run full Phase 03 manual smoke checklist

---

## 6. Out of scope

- Delete category or field
- Bulk import/export
- Preview submission form from admin

---

## 7. Validation gate

### Automated tests

- [ ] List renders active and inactive categories
- [ ] Create form validates slug format
- [ ] `photosPrivate` warning shown when checked
- [ ] Field add dialog validates required fields

### Manual smoke checklist

- [ ] Create new category with 2 fields — appears in admin list
- [ ] Category appears in reporter submission form (if active)
- [ ] Deactivate category — disappears from submission form
- [ ] Edit field label — reflected in submission form
- [ ] Non-admin cannot access `/admin/categories`

### Phase 03 final gate

Re-run all items from [PHASE-03-admin-moderation.md](./PHASE-03-admin-moderation.md#9-definition-of-done):

- [ ] Admin FIFO queue + approve/reject
- [ ] Reporter notifications
- [ ] Reject + resubmit flow
- [ ] Category management
- [ ] Admin search
- [ ] Admin email on submit (console in dev)
- [ ] `ModerationAction` audit verified

---

## 8. Exit criteria

- [ ] All Phase 03 acceptance criteria pass
- [ ] Mark sub-phase 11 and Phase 03 complete in [phase-03/README.md](./README.md)
- [ ] Ready to start Phase 04 — Browse & Discovery
