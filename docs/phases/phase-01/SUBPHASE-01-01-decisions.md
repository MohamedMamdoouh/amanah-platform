# Sub-phase 01 — Decisions & Contracts

**Status:** Not started  
**Prerequisites:** None (first sub-phase of Phase 01)

---

## 1. Summary

Resolve Section 14 blockers before any API endpoints ship. Document the API error contract, choose external service abstractions (SMS, CAPTCHA), and define phone normalization rules. This sub-phase is documentation-only — no runtime code.

---

## 2. SPEC references

| SPEC section | Topic |
| ------------ | ----- |
| Section 5.1 | Authentication (phone format, OTP rules) |
| Section 7.5 | Bot protection before OTP send |
| Section 14 | Deferred decisions (SMS provider, API error contract) |
| Section 18 | Authentication implementation notes |
| Section 20.2 | HTTP 429 + `Retry-After` pattern |

---

## 3. What you will learn

- How to design a stable, machine-readable API error contract
- Interface segregation for external services (`ISmsSender`, `ICaptchaVerifier`)
- Egyptian phone normalization rules and why they matter for identity uniqueness
- Why CAPTCHA verification must happen server-side, not client-only

**Files to read after completing:**

- [docs/api-error-contract.md](../../api-error-contract.md) — the contract you will implement in sub-phase 03

---

## 4. Deliverables

### API error contract

- Create [docs/api-error-contract.md](../../api-error-contract.md) with:
  - Response shape: `{ code, message, errors? }`
  - HTTP status mapping table
  - `Retry-After` header rules for 429 responses
  - Error code registry for Phase 01 auth/OTP codes
  - Concrete JSON examples for 400, 401, 403, 429, 503, 500

### SMS abstraction

| Decision | Choice |
| -------- | ------ |
| Interface | `ISmsSender` with `Task SendOtpAsync(string normalizedPhone, string code, CancellationToken ct)` |
| Local dev | `ConsoleSmsSender` — logs phone + code to stdout; registered when `ASPNETCORE_ENVIRONMENT=Development` |
| Production | Real provider implementation wired in sub-phase 12; provider TBD but must implement `ISmsSender` |
| Outage behavior | Throw `SmsDeliveryException`; API maps to `503` + `service.sms_unavailable` |

### CAPTCHA (bot check)

| Decision | Choice |
| -------- | ------ |
| Provider | **Cloudflare Turnstile** (free tier, privacy-friendly, no Google dependency) |
| Interface | `ICaptchaVerifier` with `Task<bool> VerifyAsync(string token, string? remoteIp, CancellationToken ct)` |
| Env vars | `TURNSTILE_SECRET_KEY` (server), `TURNSTILE_SITE_KEY` (Angular, sub-phase 11) |
| Failure | Return `400` + `auth.captcha_failed`; no OTP generated or sent |

### Phone normalization

| Input accepted | Normalized form |
| -------------- | --------------- |
| `01XXXXXXXXX` (11 digits) | `+20XXXXXXXXXX` (drop leading `0`, prepend `+20`) |
| `+20XXXXXXXXXX` | `+20XXXXXXXXXX` (as-is after digit validation) |
| `20XXXXXXXXXX` | `+20XXXXXXXXXX` (prepend `+`) |

**Validation rules:**

- Must be a valid Egyptian mobile: operator prefixes `10`, `11`, `12`, `15` after normalization
- Strip spaces, dashes, parentheses before parsing
- Arabic-Indic digits (`٠-٩`) normalized to Western (`0-9`) before validation
- `normalizedPhone` stored in DB is always E.164 `+20...` format
- Same physical number in different input formats counts as one identity for OTP limits

---

## 5. Step-by-step implementation order

1. Read SPEC Sections 5.1, 7.5, 14, 18, 20.2
2. Write [docs/api-error-contract.md](../../api-error-contract.md) with all sections and examples
3. Document `ISmsSender` and `ICaptchaVerifier` interface signatures in this file's deliverables (above) — C# interfaces created in sub-phase 06
4. Document phone normalization rules (above)
5. Document `CURRENT_TERMS_VERSION` config key (used by register validation in sub-phase 08)

---

## 6. Out of scope

- Any C# or TypeScript code
- Choosing a specific SMS vendor (deferred to sub-phase 12; interface defined here)
- Angular Turnstile widget (sub-phase 11)

---

## 7. Validation gate

### Review checklist (no automated tests)

- [ ] [docs/api-error-contract.md](../../api-error-contract.md) exists with shape, status mapping, and code registry
- [ ] Error examples documented for: 400 (validation), 401, 403 (banned), 429 (cooldown + hourly + daily), 503 (SMS outage), 500
- [ ] `Retry-After` header documented for all 429 examples
- [ ] `ISmsSender` interface and `ConsoleSmsSender` strategy documented
- [ ] `ICaptchaVerifier` + Turnstile choice documented with env var names
- [ ] Phone normalization rules cover `01X`, `+20X`, and Arabic-Indic digits

---

## 8. Exit criteria

- [ ] All review checklist items pass
- [ ] No API endpoint implementation started before this sub-phase is marked complete
- [ ] Mark sub-phase 01 complete in [phase-01/README.md](./README.md) progress table
