# Sub-phase 09 — Full Schema & Seeds

**Status:** Not started  
**Prerequisites:** [Sub-phase 08 — Sessions & Identity](./SUBPHASE-01-08-sessions.md)

---

## 1. Summary

Add the remaining Section 17 entities via a second EF Core migration, seed 8 categories with field definitions, 27 governorates, and bootstrap the admin user from `ADMIN_PHONE`. Tables exist but have no feature endpoints until later phases.

---

## 2. SPEC references

| SPEC section | Topic |
| ------------ | ----- |
| Section 5.2 | Default seed categories and field definitions |
| Section 5.3 | 27 Egyptian governorates |
| Section 5.1 | Admin bootstrap from environment variable |
| Section 17 | Full data model |
| Section 12 | Retention rules (schema supports; jobs in Phase 07) |

---

## 3. What you will learn

- Full domain model read-through before Phase 02+
- Why `ModerationAction.ReportId` is nullable (survives report deletion per Section 12)
- `photosPrivate` flag on categories and its downstream effect
- Idempotent seed migrations vs runtime seeding
- Admin bootstrap at deploy time from environment config

**Files to read after implementing:**

- `api/Data/Entities/` — all remaining entity classes (read each top-to-bottom before running migration)
- `api/Data/Configurations/` — Fluent API for each entity
- `api/Data/Migrations/` — second migration SQL
- `api/Data/Seeds/CategorySeed.cs`, `GovernorateSeed.cs`

**Suggested learning workflow:** Read each entity configuration file top-to-bottom before running the migration.

---

## 4. Deliverables

### Second migration — entities

Add tables (no API endpoints):

| Entity | Key purpose in later phases |
| ------ | --------------------------- |
| `Category` | Report categorization (Phase 02) |
| `CategoryFieldDefinition` | Per-category field schema (Phase 02) |
| `Governorate` | Location dropdown (Phase 02) |
| `Report` | Lost/found reports (Phase 02) |
| `CategoryField` | Report field values (Phase 02) |
| `ReportPhoto` | Photo metadata (Phase 02) |
| `Claim` | Ownership claims (Phase 05) |
| `Resolution` | Mutual confirmation (Phase 06) |
| `ChatThread` | Messaging (Phase 06) |
| `Message` | Chat messages (Phase 06) |
| `Notification` | In-app notifications (Phase 03) |
| `AbuseReport` | Flagging (Phase 08) |
| `ModerationAction` | Audit trail (Phase 03) |

Implement all key fields from SPEC Section 17. Use enums for status fields (`ReportStatus`, `ClaimStatus`, etc.).

### Seed data — 8 categories (SPEC 5.2)

| Category (Arabic) | Slug | photosPrivate | Required fields |
| ----------------- | ---- | ------------- | --------------- |
| هواتف | `phones` | false | Brand/model, colour |
| مستندات وهويات | `documents-ids` | **true** | Document type, first name on document |
| محافظ | `wallets` | false | Wallet type, colour |
| مفاتيح | `keys` | false | Key type, key count (integer 1–20) |
| حقائب | `bags` | false | Bag type, colour |
| إلكترونيات | `electronics` | false | Device type, brand/model |
| إكسسوارات | `accessories` | false | Accessory type |
| أخرى | `other` | false | Item type |

Default validation: text fields 2–80 chars; `first name on document` 2–40 letters/spaces only.

### Seed data — 27 governorates (SPEC 5.3)

All 27 Egyptian governorates in Arabic with sort order (alphabetical or standard listing order). Examples: القاهرة، الجيزة، الإسكندرية، … (complete list in seed file).

### Admin bootstrap

| Item | Detail |
| ---- | ------ |
| Env var | `ADMIN_PHONE` (normalized E.164 or accepted input format) |
| Behavior | On migration/seed run: if no user with this phone exists, create `User` with `Role = Admin` and placeholder display name `مدير` |
| Idempotent | Re-running seed does not duplicate admin |

---

## 5. Step-by-step implementation order

1. Read SPEC Section 17 entity table completely
2. Create entity classes for all remaining entities
3. Create Fluent API configurations (FK relationships, indexes, enums)
4. Add `DbSet<>` entries to `AmanahDbContext`
5. Run `dotnet ef migrations add FullSchema`
6. Review generated SQL — confirm FK constraints and indexes
7. Create seed data classes with category/field/governorate data
8. Implement admin bootstrap in seed or `IHostedService` at startup
9. Run `dotnet ef database update`
10. Write integration test: query seed counts

---

## 6. Out of scope

- Report submission endpoints (Phase 02)
- Admin moderation UI (Phase 03)
- Browse/search (Phase 04)
- Any CRUD API for seeded reference data (admin-editable in Phase 03)

---

## 7. Validation gate

### Automated tests

- [ ] Second migration applies cleanly on database with first migration
- [ ] Query returns exactly 8 categories
- [ ] Each category has correct `CategoryFieldDefinition` rows
- [ ] Documents/IDs category has `photosPrivate = true`
- [ ] Query returns exactly 27 governorates
- [ ] User with `ADMIN_PHONE` has `Role = Admin`
- [ ] Re-running seed is idempotent (no duplicate categories or admin)

### Manual smoke checklist

- [ ] Inspect all tables in `psql` — correct columns and FK relationships
- [ ] `Report`, `Claim`, `ChatThread` tables exist but are empty

---

## 8. Exit criteria

- [ ] All automated tests pass
- [ ] Full Section 17 schema migrated
- [ ] Seed data present
- [ ] Mark sub-phase 09 complete in [phase-01/README.md](./README.md)
