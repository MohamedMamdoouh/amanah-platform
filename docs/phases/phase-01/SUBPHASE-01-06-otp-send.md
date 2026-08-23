# Sub-phase 06 — OTP Send

**Status:** Not started  
**Prerequisites:** [Sub-phase 05 — Shared Utilities](./SUBPHASE-01-05-utilities.md)

---

## 1. Summary

Implement `POST /api/auth/otp/send` — the first real auth endpoint. Validates Egyptian phone numbers, verifies CAPTCHA, enforces send limits (120s cooldown, 2/hour, 3/Cairo day), generates and hashes a 6-digit OTP, and sends via `ConsoleSmsSender` in development.

---

## 2. SPEC references

| SPEC section | Topic |
| ------------ | ----- |
| Section 5.1 | OTP delivery, send limits, bot protection, outage behavior |
| Section 7.5 | OTP send limits and bot protection |
| Section 15.7 | Authentication acceptance criteria (send-related) |
| Section 18 | CAPTCHA before OTP send |

**Contract reference:** [docs/api-error-contract.md](../../api-error-contract.md)

---

## 3. What you will learn

- OTP generation and secure hashing (never store plaintext codes)
- Rolling window queries against `OtpCode.CreatedAt`
- Server-side CAPTCHA verification with Cloudflare Turnstile
- `ISmsSender` dependency injection and `ConsoleSmsSender` for local dev
- Mapping business rules to specific error codes and `Retry-After` values

**Files to read after implementing:**

- `api/Services/Auth/OtpService.cs`
- `api/Services/Auth/PhoneNormalizer.cs`
- `api/Services/External/ISmsSender.cs`, `ConsoleSmsSender.cs`
- `api/Services/External/ICaptchaVerifier.cs`, `TurnstileCaptchaVerifier.cs`
- `api/Controllers/AuthController.cs` (send action only)

---

## 4. Deliverables

### Endpoint

| Method | Route | Request body | Success response |
| ------ | ----- | ------------ | ---------------- |
| POST | `/api/auth/otp/send` | `{ phone, captchaToken }` | `204 No Content` |

### Request validation

| Field | Rules |
| ----- | ----- |
| `phone` | Required; normalized to E.164 `+20...`; Egyptian mobile only |
| `captchaToken` | Required; verified server-side via Turnstile |

### OTP generation flow

1. Normalize phone
2. Verify CAPTCHA → fail with `400 auth.captcha_failed`
3. Check send limits (see below) → fail with `429` + appropriate code + `Retry-After`
4. Invalidate any existing active `OtpCode` for this phone
5. Generate cryptographically random 6-digit code
6. Hash code; store `OtpCode` row with `ExpiresAt = UtcNow + 10 minutes`, `AttemptCount = 0`
7. Call `ISmsSender.SendOtpAsync(phone, code)`
8. On SMS failure → `503 service.sms_unavailable`; do not leave orphaned code (rollback or mark invalid)
9. Return `204`

### Send limits (per normalized phone)

| Limit | Window | Error code | `Retry-After` |
| ----- | ------ | ---------- | ------------- |
| Cooldown | 120 seconds since last send | `otp.cooldown` | Seconds until cooldown ends |
| Hourly | 2 sends in rolling 60 minutes | `otp.hourly_limit` | Seconds until oldest send exits window |
| Daily | 3 sends in current **Africa/Cairo calendar day** | `otp.daily_limit` | Seconds until next Cairo midnight |

**Note:** SPEC §5.1 phrases this as a "rolling day" alongside rolling-hour limits; for OTP we use a **Cairo calendar day** boundary (consistent with report/claim quotas in SPEC §3). Hourly limit remains a true rolling 60-minute window.

Use `CairoTime` helpers from sub-phase 05 for daily boundary.

**Critical:** When any limit applies, **no SMS is sent** and **no new `OtpCode` row is created**.

### Service interfaces

```csharp
public interface ISmsSender
{
    Task SendOtpAsync(string normalizedPhone, string code, CancellationToken ct = default);
}

public interface ICaptchaVerifier
{
    Task<bool> VerifyAsync(string token, string? remoteIp, CancellationToken ct = default);
}
```

- `ConsoleSmsSender`: logs `[SMS] OTP for +20...: 123456` to stdout
- `TurnstileCaptchaVerifier`: POST to Cloudflare siteverify API with `TURNSTILE_SECRET_KEY`
- `FakeCaptchaVerifier`: always returns `true` in `Testing` environment (for automated tests)

---

## 5. Step-by-step implementation order

1. Implement `PhoneNormalizer` (rules from sub-phase 01)
2. Create `ISmsSender`, `ConsoleSmsSender`
3. Create `ICaptchaVerifier`, `TurnstileCaptchaVerifier`, `FakeCaptchaVerifier`
4. Implement `OtpService.SendAsync(phone, captchaToken)`
5. Add send-limit query methods using `OtpCode.CreatedAt` and `CairoTime`
6. Add `AuthController` with `SendOtp` action only
7. Register services in DI
8. Write integration tests with `FakeCaptchaVerifier` and `ConsoleSmsSender` (or test double)
9. Manual test: call endpoint, read OTP from console

---

## 6. Out of scope

- `POST /api/auth/otp/verify` (sub-phase 07)
- Register, login, JWT (sub-phase 08)
- Angular login UI (sub-phase 11)
- Real SMS provider (sub-phase 12)

---

## 7. Validation gate

### Automated tests

- [ ] Valid phone + passed CAPTCHA → `204`; `OtpCode` row created; SMS sender called once
- [ ] Invalid phone format → `400 auth.invalid_phone`; no `OtpCode` row; no SMS
- [ ] Failed CAPTCHA → `400 auth.captcha_failed`; no `OtpCode` row; no SMS
- [ ] Resend within 120s → `429 otp.cooldown` + `Retry-After`; no SMS
- [ ] 3rd send in same Cairo day (after 2 prior) → `429 otp.daily_limit`; no SMS
- [ ] 3rd send in rolling hour (after 2 prior) → `429 otp.hourly_limit`; no SMS
- [ ] SMS provider throws → `503 service.sms_unavailable`; no access granted
- [ ] Arabic-Indic digit input `٠١٢٣٤٥٦٧٨٩` normalized and accepted if valid

### Manual smoke checklist

- [ ] `curl -X POST /api/auth/otp/send` with valid body → `204`; OTP appears in API console log
- [ ] Second immediate request → `429` with wait message

---

## 8. Exit criteria

- [ ] All automated tests pass (SPEC 15.7 send-related criteria covered)
- [ ] Manual smoke complete
- [ ] Mark sub-phase 06 complete in [phase-01/README.md](./README.md)
