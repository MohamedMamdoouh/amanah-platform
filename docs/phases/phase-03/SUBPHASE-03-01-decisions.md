# Sub-phase 01 — Moderation Decisions & Domain Types

**Status:** Not started  
**Prerequisites:** Phase 02 complete

---

## 1. Summary

Resolve the Section 14 transactional email provider decision and define moderation domain types: rejection reasons, moderation decisions, and notification type constants. No endpoints yet — enums, interfaces, and a short decisions doc only.

---

## 2. SPEC references

| SPEC section | Topic |
| ------------ | ----- |
| Section 5.5 | Eight predefined rejection reasons |
| Section 5.7 | Admin email channel; `ReportApproved`, `ReportRejected` types |
| Section 8 | Status transitions |
| Section 12 | `ModerationAction` audit |
| Section 14 | Transactional email provider |
| Section 20.1 | Notification payload contract |

---

## 3. What you will learn

- Why admin email is separate from in-app notifications (SPEC 5.7)
- Mapping SPEC rejection reasons to a C# enum with stable API values
- `ModerationAction` nullable `ReportId` — survives report deletion
- Choosing a transactional email provider for low-volume admin alerts

**Files to read after implementing:**

- `docs/phases/phase-03/SUBPHASE-03-01-decisions.md` (this doc — fill in your choice)
- `api/Domain/Moderation/RejectionReason.cs`
- `api/Domain/Moderation/ModerationDecision.cs`
- `api/Domain/Notifications/NotificationTypes.cs`
- `api/Services/External/IEmailSender.cs` (interface stub)

---

## 4. Deliverables

### Decision record

Document your choice in a comment block at the top of `IEmailSender.cs` or a `docs/decisions/email-provider.md` snippet:

| Option | Pros | Cons |
| ------ | ---- | ---- |
| **Resend** | Simple API, generous free tier | External dependency |
| **SendGrid** | Mature | Heavier setup |
| **Amazon SES** | Cheap at scale | More config |
| **ConsoleEmailSender** (dev only) | Zero setup | Not for production |

**Recommendation for v1:** Resend with `ConsoleEmailSender` for local dev (mirrors Phase 01 SMS pattern).

### `RejectionReason` enum (SPEC 5.5)

| Value | Arabic label (for API/UI) |
| ----- | ------------------------- |
| `UnclearPhotos` | صور غير واضحة |
| `SuspectedSpamOrScam` | يشتبه في كونه احتيالاً أو بريداً مزعجاً |
| `DuplicateReport` | بلاغ مكرر |
| `InsufficientDescription` | وصف غير كافٍ |
| `ContainsContactInfo` | يحتوي على معلومات تواصل |
| `ProhibitedOrIllegalItem` | عنصر محظور أو غير قانوني |
| `WrongCategory` | فئة خاطئة |
| `ContainsRawIdNumber` | يحتوي على رقم هوية/وثيقة |

Store enum as string in DB (`HasConversion<string>()`).

### `ModerationDecision` enum

- `Approved`
- `Rejected`

### `NotificationTypes` constants (Phase 03 scope only)

- `ReportApproved`
- `ReportRejected`

Payload shape per SPEC 20.1:

```json
{
  "type": "ReportApproved",
  "createdAt": "2026-08-23T00:00:00Z",
  "deepLink": "/my/reports/{id}",
  "reportId": "..."
}
```

For `ReportRejected`, include `rejectionReason` and `rejectionNote` in payload JSON (extension for reporter UI).

### `IEmailSender` interface stub

```csharp
public interface IEmailSender
{
    Task SendAdminModerationAlertAsync(int pendingCount, CancellationToken ct = default);
}
```

---

## 5. Step-by-step implementation order

1. Read SPEC 5.5 rejection reasons — confirm Arabic labels with product intent
2. Choose email provider; document decision
3. Create enum files under `api/Domain/`
4. Add `NotificationTypes` constants
5. Add `IEmailSender` interface (no implementation yet)
6. Verify `ModerationAction` entity from Phase 01 has: `ReportId?`, `AdminUserId`, `Decision`, `RejectionReason?`, `Note`, `CreatedAt`
7. No controller or service implementation in this sub-phase

---

## 6. Out of scope

- Notification API (sub-phase 02)
- Email sending implementation (sub-phase 05)
- Approve/reject logic (sub-phase 04)

---

## 7. Validation gate

### Automated tests

- [ ] Enum values serialize to expected API strings
- [ ] All 8 rejection reasons present

### Manual smoke checklist

- [ ] Decision documented in repo
- [ ] `ModerationAction` entity reviewed — fields match SPEC 17

---

## 8. Exit criteria

- [ ] Email provider chosen and documented
- [ ] Domain enums and notification type constants committed
- [ ] Mark sub-phase 01 complete in [phase-03/README.md](./README.md)
