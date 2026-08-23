# Sub-phase 01 — Search Index Migration

**Status:** Not started  
**Prerequisites:** Phase 02 complete (search column populated on write)

---

## 1. Summary

Add PostgreSQL `pg_trgm` extension and a GIN trigram index on `Report.normalizedSearchText`. Migration only — no API endpoints. Prepares the database for performant keyword search in sub-phase 02.

---

## 2. SPEC references

| SPEC section | Topic |
| ------------ | ----- |
| Section 16 | `pg_trgm` GIN index on search column |

---

## 3. What you will learn

- Enabling Postgres extensions in EF Core migrations (`migrationBuilder.Sql`)
- Why `pg_trgm` + GIN supports `ILIKE '%term%'` at scale
- Verifying index exists without running full browse queries yet
- Why backfill is not needed (column written since Phase 02)

**Files to read after implementing:**

- `api/Data/Migrations/` — new migration file
- Raw SQL: `CREATE EXTENSION IF NOT EXISTS pg_trgm;`
- Raw SQL: `CREATE INDEX ... ON "Reports" USING gin ("normalized_search_text" gin_trgm_ops);`

---

## 4. Deliverables

### Migration

1. `CREATE EXTENSION IF NOT EXISTS pg_trgm;`
2. GIN index on `Reports.normalized_search_text`:

```sql
CREATE INDEX IF NOT EXISTS ix_reports_normalized_search_text_trgm
ON "Reports" USING gin ("normalized_search_text" gin_trgm_ops);
```

### Verification query (manual or test comment)

```sql
SELECT indexname FROM pg_indexes
WHERE tablename = 'Reports' AND indexname LIKE '%trgm%';
```

### No application code changes

- Do not add browse service or controller in this sub-phase
- Do not modify `ArabicNormalizer` (Phase 01)

---

## 5. Step-by-step implementation order

1. Generate EF Core migration: `AddSearchTrigramIndex`
2. Add `migrationBuilder.Sql` for extension + index (EF cannot model GIN index natively)
3. Apply migration locally: `dotnet ef database update`
4. Verify extension and index in psql or Railway console
5. Confirm existing reports have non-null `normalized_search_text` (spot-check 2–3 rows)

---

## 6. Out of scope

- Browse query logic (sub-phase 02)
- Full-text `tsvector` (post-v1 per SPEC)
- Recomputing search column (Phase 02 responsibility)

---

## 7. Validation gate

### Automated tests

- [ ] Migration applies cleanly on empty DB and on DB with seeded reports
- [ ] Migration `Down` drops index (extension may remain — acceptable)

### Manual smoke checklist

- [ ] `\dx` in psql shows `pg_trgm` enabled
- [ ] Index appears in `pg_indexes`
- [ ] No errors on `dotnet ef database update`

---

## 8. Exit criteria

- [ ] Index migration committed and applied locally
- [ ] Mark sub-phase 01 complete in [phase-04/README.md](./README.md)
