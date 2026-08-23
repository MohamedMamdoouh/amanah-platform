# Sub-phase 07 — OTP Verify

**Status:** Not started  
**Prerequisites:** [Sub-phase 06 — OTP Send](./SUBPHASE-01-06-otp-send.md)

---

## 1. Summary

Implement `POST /api/auth/otp/verify` — validates the 6-digit OTP against the stored hash, enforces the 3-attempt limit, and returns whether the phone belongs to an existing user or needs signup (provisional token).

---

## 2. SPEC references

| SPEC section | Topic |
| ------------ | ----- |
| Section 5.1 | OTP code rules (6 digits, 10 min expiry, 3 attempts, account creation point) |
| Section 15.7 | Verification attempt limit; account creation point |
| Section 18 | Provisional token for new users before register |

**Contract reference:** [docs/api-error-contract.md](../../api-error-contract.md)

---

## 3. What you will learn

- OTP verification state machine: active → failed attempts → void → expired
- Constant-time hash comparison to prevent timing attacks
- Provisional (signup-only) JWT vs full session JWT — different claims and lifetimes
- Distinguishing new user (no `User` row) from returning user at verify time

**Files to read after implementing:**

- `api/Services/Auth/OtpService.cs` (verify methods)
- `api/Services/Auth/ProvisionalTokenService.cs`
- `api/Controllers/AuthController.cs` (verify action)

---

## 4. Deliverables

### Endpoint

| Method | Route | Request body | Success response |
| ------ | ----- | ------------ | ---------------- |
| POST | `/api/auth/otp/verify` | `{ phone, code }` | See below |

### Success responses

**Existing user** (phone matches a `User` row):

```json
{
  "status": "existing_user",
  "provisionalToken": null
}
```

Client proceeds to `POST /api/auth/login` in sub-phase 08 (or login can be implicit — document chosen flow in implementation).

**New user** (no `User` row for this phone):

```json
{
  "status": "new_user",
  "provisionalToken": "<short-lived JWT>"
}
```

Client proceeds to `POST /api/auth/register` with this token.

### Provisional token

| Property | Value |
| -------- | ----- |
| Lifetime | 15 minutes |
| Claims | `phone` (normalized), `purpose: signup` |
| Usage | Required header/body on `POST /api/auth/register` only |
| Not a session | Cannot access protected endpoints other than register |

### Verification flow

1. Normalize phone
2. Find latest non-expired, non-void `OtpCode` for phone
3. No active code → `400 auth.otp_expired`
4. `ExpiresAt < UtcNow` → `400 auth.otp_expired`
5. `AttemptCount >= 3` → `400 auth.otp_void`
6. Hash submitted code; compare to `CodeHash`
7. Mismatch → increment `AttemptCount`; if now 3 → void code → `400 auth.otp_void`; else → `400 auth.invalid_otp`
8. Match → delete or mark code used; check if `User` exists for phone
9. Return `existing_user` or `new_user` + provisional token

### Error codes

| Condition | Code |
| --------- | ---- |
| Wrong code (attempts remaining) | `auth.invalid_otp` |
| Code expired | `auth.otp_expired` |
| 3 failed attempts | `auth.otp_void` |

---

## 5. Step-by-step implementation order

1. Add code hashing utility (if not already in sub-phase 06)
2. Implement `OtpService.VerifyAsync(phone, code)`
3. Implement `ProvisionalTokenService` (issue + validate signup JWT)
4. Add `VerifyOtp` action to `AuthController`
5. Write integration tests for all paths
6. Manual test: send OTP (sub-phase 06), verify with console code

---

## 6. Out of scope

- `User` row creation (sub-phase 08 `register`)
- Full session JWT issue (sub-phase 08 `login`)
- Angular login UI (sub-phase 11)

---

## 7. Validation gate

### Automated tests

- [ ] Correct code → success; `OtpCode` consumed/deleted
- [ ] Wrong code (1st attempt) → `400 auth.invalid_otp`; `AttemptCount` = 1
- [ ] Wrong code (3rd attempt) → `400 auth.otp_void`; code voided
- [ ] Expired code → `400 auth.otp_expired`
- [ ] Void code (after 3 failures) → `400 auth.otp_void`; new verify without re-send fails
- [ ] New phone → `status: "new_user"` + valid `provisionalToken`
- [ ] Existing user phone → `status: "existing_user"`
- [ ] Provisional token contains `purpose: signup` claim; rejected for other purposes

### Manual smoke checklist

- [ ] Send OTP → verify with console code → receive `new_user` or `existing_user` response
- [ ] Enter wrong code 3 times → must request new OTP (send again)

---

## 8. Exit criteria

- [ ] All automated tests pass (SPEC 15.7 verification criteria covered)
- [ ] Abandoned signup path understood: verify succeeds but no `User` until register (tested in sub-phase 08)
- [ ] Mark sub-phase 07 complete in [phase-01/README.md](./README.md)
