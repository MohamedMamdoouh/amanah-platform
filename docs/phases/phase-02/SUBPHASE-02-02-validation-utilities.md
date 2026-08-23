# Sub-phase 02 — Report Validation Utilities

**Status:** Not started  
**Prerequisites:** [Sub-phase 01 — Reference Data](./SUBPHASE-02-01-reference-data.md), Phase 01 [Sub-phase 05 — Utilities](../phase-01/SUBPHASE-01-05-utilities.md)

---

## 1. Summary

Implement pure validation helpers for report submission: contact-info detection, per-field category validation, date bounds, and the normalized search-text builder. No HTTP endpoints — unit tests only. These are wired into `POST /api/reports` in sub-phase 05.

---

## 2. SPEC references

| SPEC section | Topic |
| ------------ | ----- |
| Section 4.1.3 | Contact-info block patterns |
| Section 4.1 | Date lost/found bounds; text normalization |
| Section 5.2 | Category field validation rules |
| Section 5.4 | Reward amount rules |
| Section 16 | Search column normalization |

**Contract reference:** Field-level errors use `validation.failed` with `errors: { fieldName: [...] }` per [api-error-contract.md](../../api-error-contract.md)

---

## 3. What you will learn

- Regex and normalization for contact-info blocking (URLs, social domains, phone sequences)
- Arabic-Indic digit normalization before digit counting
- Why hidden verification detail is **exempt** from contact-info block
- Building denormalized search text with `ArabicNormalizer` from Phase 01
- Returning structured field errors (field key + message) for API mapping

**Files to read after implementing:**

- `api/Utilities/ContactInfoDetector.cs`
- `api/Utilities/CategoryFieldValidator.cs`
- `api/Utilities/ReportSearchTextBuilder.cs`
- `api/Utilities/RewardValidator.cs`
- `api.Tests/Utilities/ContactInfoDetectorTests.cs`
- `api.Tests/Utilities/CategoryFieldValidatorTests.cs`
- `api.Tests/Utilities/ReportSearchTextBuilderTests.cs`

---

## 4. Deliverables

### `ContactInfoDetector`

| Method | Purpose |
| ------ | ------- |
| `ContainsContactInfo(string? text)` | Returns `true` if blocked pattern found |

**Blocked patterns (SPEC 4.1.3):**

- URL-like: `http://`, `https://`, `www.`
- Social domains: `facebook.com`, `instagram.com`, `t.me`, `telegram.me`, `wa.me`, `whatsapp.com` (case-insensitive)
- Phone-like: after normalizing Arabic-Indic digits → Western, strip whitespace/dashes/dots/parentheses, **10+ consecutive digits**

**Scoped fields** (caller passes each; detector is field-agnostic):

- `title`, `description`, `area`, `itemHeldLocationDetail`, category text fields
- **Not** `hiddenVerificationDetail`

### `CategoryFieldValidator`

| Method | Purpose |
| ------ | ------- |
| `ValidateFields(category, fieldValues)` | Returns list of field errors (empty = valid) |

Rules from field definitions:

- Required fields present and non-empty after `TextNormalizer`
- Text: length within min/max (default 2–80 from seed)
- `first name on document`: 2–40, letters and spaces only (Arabic + Latin)
- Integer (`key count`): parseable, within min/max (1–20)

### `ReportDateValidator`

Uses `CairoTime.IsDateInValidRange` from Phase 01:

- Reject future dates (Cairo calendar)
- Reject dates more than 12 months before today (Cairo)
- Accept today's Cairo date

### `RewardValidator`

| Rule | Detail |
| ---- | ------ |
| Flag false | Amount must be null/empty |
| Flag true | Amount required; integer 50–50,000 EGP |

### `ReportSearchTextBuilder`

| Method | Purpose |
| ------ | ------- |
| `BuildNormalizedSearchText(report fields...)` | Concatenate title + description + public category field values + area; apply `ArabicNormalizer.NormalizeForSearch` |

Used on every report write (create in sub-phase 05; update in Phase 03 resubmit).

### Core report field length rules (for sub-phase 05)

| Field | Min | Max |
| ----- | --- | --- |
| Title | 10 | 80 |
| Description | 20 | 1,000 |
| Area | 0 (optional) | 120 |
| Held-location detail | 0 (required when Other) | 120 |
| Hidden verification detail | 10 | 500 |

---

## 5. Step-by-step implementation order

1. Implement `ContactInfoDetector` with digit normalization helper
2. Write exhaustive unit tests for URLs, social domains, Arabic-Indic phones, edge cases
3. Implement `CategoryFieldValidator` driven by `CategoryFieldDefinition` entities
4. Implement `RewardValidator`
5. Implement `ReportSearchTextBuilder` using `ArabicNormalizer`
6. Add `ReportDateValidator` wrapper if helpful (thin layer over `CairoTime`)
7. Do **not** wire into controllers yet

---

## 6. Out of scope

- `POST /api/reports` endpoint (sub-phase 05)
- Claim text contact-info block (Phase 05)
- Chat message exemption (Phase 06)
- Quota checks (sub-phase 03)

---

## 7. Validation gate

### Automated tests — `ContactInfoDetector`

- [ ] `https://example.com` in title → blocked
- [ ] `facebook.com/page` → blocked
- [ ] `٠١٢٣٤٥٦٧٨٩٠` (10 Arabic-Indic digits) → blocked
- [ ] `012-345-6789` (10 digits with separators) → blocked
- [ ] `9 digits only` → not blocked
- [ ] Same phone pattern in hidden detail → **not** blocked (caller skips hidden field)
- [ ] Normal descriptive Arabic text → not blocked

### Automated tests — `CategoryFieldValidator`

- [ ] Missing required field → error with field key
- [ ] Text field 1 char → too short
- [ ] First name with digits → pattern error
- [ ] Key count 0 and 21 → out of range
- [ ] Key count 5 → valid

### Automated tests — date and reward

- [ ] Future date rejected
- [ ] 13 months ago rejected
- [ ] Today (Cairo) accepted
- [ ] Reward flag true without amount → error
- [ ] Reward amount 49 → error; 50 → valid; 50,000 → valid; 50,001 → error

### Automated tests — `ReportSearchTextBuilder`

- [ ] Title + description + area + category values concatenated
- [ ] Alef variants in input normalize consistently
- [ ] Empty optional area omitted without double spaces

### Manual smoke checklist

- [ ] `dotnet test api.Tests --filter "FullyQualifiedName~Utilities"` passes

---

## 8. Exit criteria

- [ ] All utility tests pass
- [ ] Utilities are pure (no DB, no HTTP)
- [ ] Mark sub-phase 02 complete in [phase-02/README.md](./README.md)
