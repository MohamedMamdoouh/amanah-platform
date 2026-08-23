# Sub-phase 04 — Auth Database

**Status:** Not started  
**Prerequisites:** [Sub-phase 03 — API Plumbing](./SUBPHASE-01-03-api-plumbing.md)

---

## 1. Summary

Add EF Core with PostgreSQL and create the first migration containing auth-related entities only: `User`, `OtpCode`, and `RefreshToken`. No API endpoints yet — persistence layer and integration test only.

---

## 2. SPEC references

| SPEC section | Topic |
| ------------ | ----- |
| Section 17 | Data model (`User`, `OtpCode`, `RefreshToken` subset) |
| Section 3 | UTC `timestamptz` storage |
| Section 5.1 | User fields (phone, display name, role, ban, ToS) |
| Section 12 | OTP and session retention (schema only; cleanup job in Phase 07) |
| Section 18 | Session token storage |

---

## 3. What you will learn

- EF Core entity configuration with Fluent API (not just data annotations)
- PostgreSQL `timestamptz` for UTC timestamps
- Phone normalization at the persistence boundary
- Migration workflow: `dotnet ef migrations add` / `dotnet ef database update`
- Why OTP codes are stored hashed, not plaintext

**Files to read after implementing:**

- `api/Data/AmanahDbContext.cs`
- `api/Data/Entities/User.cs`, `OtpCode.cs`, `RefreshToken.cs`
- `api/Data/Configurations/` — Fluent API per entity
- `api/Data/Migrations/` — generated SQL review

---

## 4. Deliverables

### EF Core setup

| Item | Detail |
| ---- | ------ |
| Package | `Npgsql.EntityFrameworkCore.PostgreSQL` |
| Context | `AmanahDbContext` with `DbSet<>` for three entities |
| Connection | Read from `ConnectionStrings:Default` (Docker Postgres from sub-phase 02) |
| Registration | `AddDbContext` in `Program.cs` |

### Entities

#### `User`

| Column | Type | Notes |
| ------ | ---- | ----- |
| `Id` | `uuid` PK | Generated on insert |
| `NormalizedPhone` | `varchar(16)` | E.164 `+20...`; unique index |
| `DisplayName` | `varchar(40)` | Nullable until register completes |
| `Role` | `varchar(10)` | `User` or `Admin` enum |
| `IsBanned` | `boolean` | Default `false` |
| `BanReason` | `text` | Nullable |
| `TermsVersion` | `varchar(20)` | Nullable until register |
| `TermsAcceptedAt` | `timestamptz` | Nullable until register |
| `CreatedAt` | `timestamptz` | UTC, set on insert |

#### `OtpCode`

| Column | Type | Notes |
| ------ | ---- | ----- |
| `Id` | `uuid` PK | |
| `Phone` | `varchar(16)` | Normalized phone; indexed |
| `CodeHash` | `varchar(128)` | SHA-256 or bcrypt hash of 6-digit code |
| `ExpiresAt` | `timestamptz` | UTC; 10 minutes from creation |
| `AttemptCount` | `int` | Default 0; max 3 before void |
| `CreatedAt` | `timestamptz` | UTC; used for send-limit queries |

#### `RefreshToken`

| Column | Type | Notes |
| ------ | ---- | ----- |
| `Id` | `uuid` PK | |
| `UserId` | `uuid` FK → User | Indexed |
| `TokenHash` | `varchar(128)` | Hash of opaque refresh token |
| `ExpiresAt` | `timestamptz` | UTC; 30 days from issue |
| `IsRevoked` | `boolean` | Default `false` |
| `CreatedAt` | `timestamptz` | UTC |

### Indexes

- `User.NormalizedPhone` — unique
- `OtpCode.Phone` — non-unique (multiple codes over time)
- `RefreshToken.UserId` — non-unique

### Migration

- Name: `InitialAuth`
- Applies cleanly to empty local Postgres database

---

## 5. Step-by-step implementation order

1. Add EF Core packages to `api/`
2. Create entity classes in `api/Data/Entities/`
3. Create Fluent API configurations in `api/Data/Configurations/`
4. Implement `AmanahDbContext`
5. Register `DbContext` in `Program.cs`
6. Run `dotnet ef migrations add InitialAuth`
7. Review generated migration SQL — confirm `timestamptz` columns
8. Run `dotnet ef database update`
9. Write integration test: insert `User`, query by `NormalizedPhone`

---

## 6. Out of scope

- Report, Claim, Chat, and other Section 17 entities (sub-phase 09)
- Seed data (categories, governorates, admin — sub-phase 09)
- Auth API endpoints
- OTP hashing service (sub-phase 06)

---

## 7. Validation gate

### Automated tests

- [ ] Migration applies to empty database without error
- [ ] Integration test inserts a `User` with normalized phone `+201012345678` and retrieves it
- [ ] Unique constraint on `User.NormalizedPhone` rejects duplicate insert
- [ ] `OtpCode` and `RefreshToken` can be inserted with valid FK to `User`

### Manual smoke checklist

- [ ] `dotnet ef database update` succeeds against Docker Postgres
- [ ] Inspect tables in `psql`: correct column types and indexes exist

---

## 8. Exit criteria

- [ ] All automated tests pass
- [ ] Migration file committed
- [ ] Mark sub-phase 04 complete in [phase-01/README.md](./README.md)
