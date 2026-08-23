# Sub-phase 05 — Shared Utilities

**Status:** Not started  
**Prerequisites:** [Sub-phase 04 — Auth Database](./SUBPHASE-01-04-auth-db.md)

---

## 1. Summary

Implement pure utility functions for timezone handling, text normalization, and Arabic search normalization. These are used by OTP send limits (sub-phase 06) and later phases (report validation, search). No endpoint wiring in this sub-phase — unit tests only.

---

## 2. SPEC references

| SPEC section | Topic |
| ------------ | ----- |
| Section 3 | Africa/Cairo day boundaries for quotas; UTC storage |
| Section 4.1 | Text normalization (trim + collapse spaces) |
| Section 16 | Arabic search normalization rules |

---

## 3. What you will learn

- `TimeZoneInfo.FindSystemTimeZoneById("Africa/Cairo")` and Cairo calendar day boundaries
- Why quotas use Cairo local midnight while the database stores UTC
- Arabic text normalization for search (alef variants, taa marbuta, tatweel, diacritics)
- Writing deterministic pure functions with comprehensive unit tests

**Files to read after implementing:**

- `api/Utilities/CairoTime.cs`
- `api/Utilities/TextNormalizer.cs`
- `api/Utilities/ArabicNormalizer.cs`
- `api.Tests/Utilities/` — unit test files

---

## 4. Deliverables

### `CairoTime` helper

| Method | Purpose |
| ------ | ------- |
| `GetCairoTimeZone()` | Returns `TimeZoneInfo` for `Africa/Cairo` |
| `UtcNow()` | Current UTC `DateTimeOffset` |
| `ToCairoDate(DateTimeOffset utc)` | Convert UTC instant to Cairo calendar date |
| `GetCairoDayStartUtc(DateTimeOffset utc)` | Start of current Cairo calendar day as UTC `DateTimeOffset` |
| `GetCairoDayEndUtc(DateTimeOffset utc)` | End of current Cairo calendar day as UTC `DateTimeOffset` |
| `GetRollingHourStartUtc(DateTimeOffset utc)` | UTC instant 1 hour ago (for hourly OTP limit) |
| `IsDateInValidRange(DateOnly date, int maxMonthsAgo)` | For report date validation (Phase 02); not future, not > 12 months ago |

### `TextNormalizer`

| Method | Purpose |
| ------ | ------- |
| `Normalize(string? input)` | Trim leading/trailing whitespace; collapse internal runs of whitespace to single space; return empty string for null/whitespace-only |

### `ArabicNormalizer`

Per SPEC Section 16 normalization rules (applied identically to stored text and queries):

| Transformation | Detail |
| -------------- | ------ |
| Alef variants | `أ إ آ ٱ` → `ا` |
| Yaa | `ى` → `ي` |
| Taa marbuta | `ة` → `ه` |
| Tatweel | Strip `ـ` |
| Diacritics | Strip Arabic diacritic marks (tashkeel) |
| Whitespace | Collapse runs via `TextNormalizer` |
| Case | Lowercase (for Latin characters in mixed text) |

| Method | Purpose |
| ------ | ------- |
| `NormalizeForSearch(string? input)` | Apply all rules above |
| `BuildSearchTerms(string query)` | Normalize, split on whitespace, return non-empty terms (for Phase 04) |

---

## 5. Step-by-step implementation order

1. Create `api/Utilities/CairoTime.cs` with timezone helpers
2. Create `api/Utilities/TextNormalizer.cs`
3. Create `api/Utilities/ArabicNormalizer.cs`
4. Create `api.Tests/Utilities/CairoTimeTests.cs` with edge cases
5. Create `api.Tests/Utilities/TextNormalizerTests.cs`
6. Create `api.Tests/Utilities/ArabicNormalizerTests.cs` with alef/hamza/tatweel pairs
7. Do not wire into any endpoint yet

---

## 6. Out of scope

- Wiring utilities into OTP endpoints (sub-phase 06)
- Report date validation endpoint (Phase 02)
- Search query execution (Phase 04)
- Angular changes

---

## 7. Validation gate

### Automated tests — `CairoTime`

- [ ] `GetCairoDayStartUtc` at 23:59 Cairo returns correct UTC boundary
- [ ] `GetCairoDayStartUtc` at 00:01 Cairo (next day) returns a different day start than 23:59
- [ ] `GetRollingHourStartUtc` returns instant exactly 1 hour before given UTC time
- [ ] `IsDateInValidRange` rejects future dates
- [ ] `IsDateInValidRange` rejects dates more than 12 months ago (Cairo calendar)

### Automated tests — `TextNormalizer`

- [ ] `"  hello   world  "` → `"hello world"`
- [ ] `null` and `""` → `""`

### Automated tests — `ArabicNormalizer`

- [ ] `"أحمد"` and `"احمد"` normalize to same string
- [ ] `"مدرسة"` and `"مدرسه"` normalize to same string
- [ ] Tatweel stripped: `"مـحـمد"` → `"محمد"`
- [ ] Diacritics stripped
- [ ] `BuildSearchTerms("  كلمة   ثانية ")` → `["كلمة", "ثانية"]`

### Manual smoke checklist

- [ ] All unit tests pass: `dotnet test api.Tests`

---

## 8. Exit criteria

- [ ] All automated tests pass
- [ ] Utilities are pure (no DB, no HTTP, no static mutable state)
- [ ] Mark sub-phase 05 complete in [phase-01/README.md](./README.md)
