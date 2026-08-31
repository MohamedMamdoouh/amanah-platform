# Amanah - Specification (SPEC)

**Status:** v9  
**Owner:** Mohamed Mamdouh

- **Part I (1-15)** - product behavior.
- **Part II (16-21)** - technical specification.

---

## Table of contents

| Section | Topic                       |
| ------- | --------------------------- |
| 1       | Problem statement           |
| 2       | Vision & goals              |
| 3       | Target users & launch scope |
| 4       | Core user flows             |
| 5       | Feature specification       |
| 6       | Verification & claim model  |
| 7       | Abuse, safety & enforcement |
| 8       | Report lifecycle            |
| 9       | Permissions matrix          |
| 10      | Out of scope                |
| 11      | Non-functional requirements |
| 12      | Data retention              |
| 13      | Risks & mitigations         |
| 14      | Deferred decisions          |
| 15      | Acceptance criteria         |
| 16      | Architecture & stack        |
| 17      | Data model                  |
| 18      | Authentication & sessions   |
| 19      | Uploads & media processing  |
| 20      | API contracts               |
| 21      | Infrastructure & operations |

---

# Part I - Product Requirements

---

## 1. Problem Statement

In Egypt, when someone loses an item (a phone, national ID, wallet, keys, a bag, etc.), or finds someone else's lost item, there is no dedicated, trustworthy channel to connect the two parties. Today this happens informally through scattered Facebook posts and groups, which has several problems:

- No moderation - posts can be spam, scams, or joke posts.
- No structured data - no consistent way to search by location, category, or date.
- No verification - no way to confirm a claimant is the real owner before handing over a found item, creating a real risk of theft or fraud.
- No direct communication channel - people have to exchange personal contact info immediately, with no intermediary layer of safety.

**Amanah** aims to be the dedicated home for these reports across Egypt, with built-in moderation, a lightweight ownership-verification mechanism, and in-app messaging.

---

## 2. Vision & Goals

Build a **real product** (not just a portfolio demo) that real people across Egypt can use to report lost items, report found items, and reconnect - safely and with minimal friction.

### Primary success metric

**Number of confirmed reunions** - an item successfully returned to its owner through the platform. A reunion is only counted when **both parties mutually confirm** (see 4.6). This is the truest signal that the platform delivered real value, as opposed to vanity metrics like report volume or signups.

---

## 3. Target Users & Launch Scope

- **Real users**, not a simulated/demo audience - the platform is meant to actually be used.
- **Nationwide launch (all of Egypt).** No single pilot city. Density of overlapping "lost" and "found" reports in the same area is normally the argument for starting narrow, but expected early traffic is low (pre-marketing, solo-founder scale), so that dilution risk is acceptable and manual moderation stays feasible. Revisit with geo-targeted seeding if thin coverage in some governorates becomes a real bottleneck.
- **Language:** Arabic UI for v1 (RTL layout). Catalog data (categories, governorates, field keys) is stored as English keys in the database; Arabic display labels live in frontend translation files (`assets/i18n/ar/`). User-submitted content (reports, chat, display names) is stored as entered.
- **Access model:** anyone, including logged-out visitors, can browse, search, and filter all publicly visible reports (see 4.4 for which statuses are publicly visible). **Login is required** to: submit a report, submit a claim, respond to a claim, message another user, confirm a resolution, or flag a listing. Action buttons prompt login on tap.
- **Logged-out vs logged-in parity:** logged-out visitors see exactly the same content as logged-in users on publicly visible reports.
- **Public identity:** each user chooses a **display name** at signup, which is shown on their listings and in chat. Display names are **not unique** in v1 and are **not editable** after signup. Phone numbers are **never** shown to other users - admin only.
- **Eligibility:** users must explicitly accept the Terms of Service and Privacy Policy at signup. Minimum age stated in Terms: **16**. No separate age verification in v1.
- **Time standard:** all `timestamptz` values are stored in **UTC** in the database. **Date-only** fields (date lost/found) are stored as PostgreSQL `date` values representing the **Africa/Cairo calendar day** the user selected. **Day boundaries** for quotas and date validation use the Africa/Cairo calendar. **Displayed** timestamps use Africa/Cairo local time (11.2).
- **Support channel:** a static contact page, linked from the footer, publishes a support email address. It is reachable by logged-out visitors. Every requirement in this document that refers to "contacting the admin" means this channel.

---

## 4. Core User Flows

### 4.1 Reporting a lost item

1. User signs up/logs in via phone number + OTP and sets a display name; accepts Terms (5.1).
2. User selects **"I lost something"**, picks a category, and fills in:

- **Title** (required, 10-80 characters).
- **Description** (required, 20-1,000 characters).
- **Date lost** (required, exact date - no time). Must not be in the future and must not be more than **12 months** ago.
- Category-specific fields (5.2).
- **Location:** governorate (required dropdown) + free-text area/landmark (optional, max 120 chars; e.g. "Ramses station, platform 2").
- **Photos:** optional, 0-5, max 5 MB each. Visibility is category-driven, not user-controlled (5.2). Uploaded photos have location/device metadata removed; users see optimized thumbnails.
- Optional **reward offered** flag with amount in EGP (5.4).
- **One hidden verification detail (required)** - a private text fact (10-500 characters), never shown to anyone but the reporter. Used in the claim flow (6).

1. **Contact-info block.** Submission is rejected with field-level validation if contact info is detected in any public text field - `title`, `description`, `area/landmark`, found-item held-location detail, and all public category text fields - or in claim text (6.2). Blocking patterns are:

- URL-like text (`http://`, `https://`, `www.`), or explicit social domains (`facebook.com`, `instagram.com`, `t.me`, `telegram.me`, `wa.me`, `whatsapp.com`).
- Phone-like sequences that normalize to **10 or more digits** (Arabic-Indic and Western digits are normalized to Western digits, and whitespace/dashes/dots/parentheses are stripped, before counting).

This is a hard block with no override - the user must remove the flagged text to submit. It re-runs on every resubmission (4.3). It does **not** apply to the hidden verification detail or to chat messages (5.6). Admin review remains a backstop for anything that slips through. See risk table (section 13) for rationale. All user-entered text fields are normalized by trimming leading/trailing spaces and collapsing repeated internal spaces before validation and storage.

1. Report is submitted with status **Pending Review**. The confirmation screen says review is **"usually within a day"** (internal SLA: 24 hours - not a guaranteed promise).
2. No draft saving in v1 - submission is a single session.
3. **Submission quota:** max **3 new reports per day** per account (Africa/Cairo day boundary). Resubmissions of a rejected report do not count against this quota and are capped separately (4.3).
4. **Concurrent open-report cap:** max **5** reports per account in `Pending Review`, `Published`, or `Claim In Progress`. `Rejected` and terminal statuses do not count. Resubmitting a `Rejected` report does not require a free slot. New submissions at the cap are rejected with clear messaging.

### 4.2 Reporting a found item

Same as 4.1, with these differences:

- User selects **"I found something"** and enters **date found** (same rules as date lost).
- **Where the item is held:** required dropdown (With finder / Police station / Building security / Workplace / Other) plus free-text detail (max 120 chars). Detail is required when the dropdown is `Other`. Shown on the public listing.

### 4.3 Admin review, rejection, and resubmission

1. Admin is notified of new submissions in-app and by email (5.7). The queue is worked **oldest first** (FIFO), with a pending-count badge.
2. Admin **approves** → report becomes **Published** and publicly visible.
3. Admin **rejects** → selects a reason from the predefined list (5.5) and may add an optional note shown to the reporter. The report becomes **Rejected**: it leaves the queue, is never publicly visible, and is readable only by its reporter and the admin. The reporter is notified with the reason and note.
4. **Fix and resubmit.** From a `Rejected` report the reporter may edit the content and resubmit it, which returns it to `Pending Review`.

- Resubmission does **not** count against the daily submission quota (4.1.6).
- A report may be resubmitted at most **3 times**. After the 3rd resubmission is rejected, the report is final and cannot be resubmitted again; the reporter may still submit a brand-new report under the normal quota and open-report cap.
- Every field may be changed, including the category. Photo visibility is re-derived from the category's `photosPrivate` flag on resubmission (5.2).
- The contact-info block (4.1.3) re-runs on every resubmission.

1. A `Rejected` report and its photos are deleted after 30 days if never resubmitted (section 12).
2. **No editing while `Pending Review`.** Once submitted, a report cannot be edited until the admin decides, because the admin may be mid-review. Editing is possible only from `Rejected`.
3. **No editing after publication.** Published content is fixed. A reporter who needs to change published content withdraws the report (4.7) and submits a new one.
4. Admin may **take down** an already-published report or **ban** a user (7.2).

The admin never edits report content - only approves, rejects, and takes enforcement actions.

### 4.4 Browsing & discovery

- **Publicly visible statuses:** `Published` and `Claim In Progress`. Both appear in browse, search, and filters, and both have a publicly readable detail page. A `Claim In Progress` listing is clearly labelled as having a claim in progress and cannot be claimed (6.5).
- **Keyword search:** matches the report's title, description, public category field values, and area/landmark text. All words in the query must appear (in any order), case-insensitively. Arabic text is normalized before matching so that variants of alef/hamza, taa marbuta and haa, tatweel, and diacritics match each other.
- **Filters:** category, governorate, lost vs found, and a date-lost/date-found **range** (within the last 12 months). Filters combine with the keyword search.
- **Sort:** newest published first.
- **Pagination:** numbered pages, 20 reports per page.
- **Public URLs:** `/lost/{id}` and `/found/{id}`. No URL slugs or social link previews in v1.
- **URL access by status:** `Pending Review` and `Rejected` → not-found page for everyone except reporter and admin. `Resolved`, `Withdrawn`, and `Removed by Admin` → permanently-unavailable page. Missing IDs and wrong-type links → not-found page.
- The admin's moderation search uses the same keyword search, additionally scoped to include `Pending Review` and `Rejected` reports, to help catch duplicates before approval.

### 4.5 Claiming / verification

See section 6.

### 4.6 Handover & resolution

1. Verified parties coordinate via in-app chat. A **safety banner** appears at the top of every new chat thread (meet in public, prefer police stations, never pay in advance). A linked safety page is available from the footer.
2. Chat supports text and photo attachments (5.6).
3. Once the item is returned, **both parties** tap **"Confirm Resolved"**. When the first party confirms, the other party is notified that their confirmation is awaited. When both confirmations are recorded, the report becomes **Resolved** and both parties are notified.
4. **A confirmation is irrevocable.** A party who has confirmed cannot withdraw that confirmation and cannot cancel the claim. The party who has not yet confirmed may still cancel.
5. Either party may **cancel** an approved claim while neither has confirmed, or the not-yet-confirmed party may cancel after the other has confirmed → the report returns to `Published` and becomes claimable again (6.7).
6. Resolution is **final** - no dispute or reopen mechanism in v1. Either party can flag the listing (7.1) or use the support channel (section 3) if something goes wrong.
7. After resolution or cancellation, chat becomes read-only immediately and is deleted after 30 days (section 12).

### 4.7 Withdrawal and listing lifecycle

- **Automatic expiry.** A `Published` report is automatically withdrawn after **90 cumulative days** in `Published` status. The timer **pauses** while the report is `Claim In Progress` and **resumes** with remaining time if the claim is cancelled and the report returns to `Published`. `Pending Review` and `Rejected` reports never auto-expire. **7 days before** expiry the reporter receives an in-app `ReportExpiringSoon` notification. On expiry the report becomes `Withdrawn` with system reason `_expired_` (not shown in the reporter's optional reason picker); pending claims are closed (6.7); reporter and affected claimants are notified (`ReportExpired`). Disclosed in the Terms and listing copy. Listing expiry applies only while `Published`; the 10-day pending-claim timeout (6.3) is independent and applies only to `Pending` claims.
- The reporter may withdraw a report while its status is `Pending Review` or `Published`. If a claim is already approved (`Claim In Progress`), the claim must be cancelled first (6.7).
- On withdrawal the reporter may optionally select a reason: _recovered outside the platform_, _no longer needed_, _posted by mistake_, or _other_. The reason is internal (reporter and admin only).
- Withdrawal closes any `Pending` claims on the report (6.7).
- Withdrawn reports are hidden from discovery and their URLs show a permanently-unavailable page (4.4).
- Reporters can always submit a new report, subject to the daily quota and open-report cap (4.1.6-4.1.7).

### 4.8 Self-management surfaces

- **My Reports** - status tabs: `Pending Review`, `Rejected`, `Published`, `Claim In Progress`, and closed (`Withdrawn`, `Resolved`, `Removed by Admin`). The `Rejected` tab is where a reporter reads the rejection reason and fixes and resubmits (4.3).
- **My Claims** - every claim the user submitted, with its status and outcome.
- **My Chats** - every chat thread the user is part of, active or read-only, reachable regardless of the underlying report's status until the thread is deleted (section 12).
- Reporter claim-review actions live inside the corresponding report-detail page, in a dedicated **Claims** section.

---

## 5. Feature Specification (v1 scope)

### 5.1 Authentication

- **Method:** phone number + OTP as the sole signup/login method.
- **Accepted phone inputs:** Egyptian mobile numbers only (`01XXXXXXXXX` or international `+20XXXXXXXXXX`). Formatting variants of the same number count as one identity for uniqueness and OTP send limits.
- **Display name rules:** required at signup, 3-40 chars, Arabic/Latin letters + digits + spaces + `- _ .`; not unique; not editable after signup in v1.
- **Account creation point:** an account exists only once the user has submitted a display name and accepted the Terms. Abandoning signup after OTP verification leaves no account behind.
- **OTP delivery:** SMS one-time code.
- **Code rules:** 6-digit code, valid **10 minutes**; up to **3** entry attempts per code; after 3 failures the code is void and the user must request a new one (which counts toward send limits).
- **Send limits (per phone, rolling windows):**
  - New code / resend only after **120 seconds** since the last send.
  - Max **2** sends per rolling hour.
  - Max **3** sends per rolling day.
  - When a limit applies, **no SMS is sent**; the user sees a clear message explaining the limit and when they can retry.
- **Bot protection:** a bot check must be passed **before** an OTP can be sent.
- **Outage behavior:** if the verification service is unavailable, signup/login is blocked with a clear temporary-unavailable message.
- **Banned accounts:** a banned user cannot sign in. The blocked sign-in attempt states that the account is banned and shows the recorded reason (7.2).
- **Sessions:** users stay signed in across multiple devices. "Log out everywhere" ends all sessions immediately. Sessions expire after a period without use and then require re-authentication.
- **Terms changes:** there is no re-acceptance flow in v1.
- **Phone change:** out of scope in v1 - neither self-serve nor admin-assisted.
- **Account deletion:** self-serve, but **blocked** while the user has a report in `Claim In Progress` or holds an approved claim on someone else's report - the claim must be cancelled first (6.7). Once eligible: their reports in `Pending Review` or `Published` are withdrawn (closing any pending claims on them), their own `Pending` claims are withdrawn, and they are signed out immediately. Chat message bodies remain until the normal chat retention deadline while sender identity is anonymized immediately. Direct personal data is purged within **30 days** per section 12.
- **Roles:** `User`, `Admin`.
- **Admin bootstrap:** the initial admin account is provisioned at launch; no in-app admin promotion in v1.

### 5.2 Report categories & fields

Categories and their field definitions are **admin-managed** (5.5). Eight categories are seeded at deploy with the field definitions below. All category text fields are normalized (trim + collapse repeated spaces) before validation and storage, and are covered by the contact-info block (4.1.3).

**Default seed categories and fields:**

| Category      | Required public fields                | Private photos |
| ------------- | ------------------------------------- | -------------- |
| Phones        | Brand/model, colour                   | No             |
| Documents/IDs | Document type, first name on document | Yes            |
| Wallets       | Wallet type, colour                   | No             |
| Keys          | Key type, key count                   | No             |
| Bags          | Bag type, colour                      | No             |
| Electronics   | Device type, brand/model              | No             |
| Accessories   | Accessory type                        | No             |
| Other         | Item type                             | No             |

**Default validation rules** (apply to seed data and admin-defined fields unless overridden per field):

- Text fields: **2-80** characters.
- `First name on document` (seed): **2-40** characters, letters and spaces only. First name only - helper copy states that surnames must not be entered.
- `Key count` (seed): integer **1-20**.

**Private-photo categories (`photosPrivate` = true):**

- All photos on such a report are private. Private photos are visible only to the reporter and, for review or enforcement investigation, the admin. They never appear on the public listing and are never shown to claimants.
- The seeded Documents/IDs category uses `photosPrivate`. Its public identifying signal is document type plus first name on the document, which lets an owner recognize their own document without exposing a full identity.
- No raw ID-number field exists, and a raw ID/document number typed into any text field is grounds for rejection (5.5 reason 8). The contact-info block stops most of these; the rejection reason covers shorter identifiers that slip through.

**Hidden verification detail (required on every report):**

- Exactly one per report: private text, **10-500 characters**.
- Visible **only to the reporter**. Never shown to claimants, the public, or the admin.
- **Rationale:** this is the reporter's private proof question - like a security answer. Admin moderates public content quality; ownership verification stays between reporter and claimant. Showing it to admin would expand PII access and undermine reporter trust.
- Claims use one model for all reports: the claimant describes the item (6.2) and the reporter compares that description manually against the report details and this hidden detail.
- Editable only from `Rejected` (4.3.4); not while `Pending Review`, `Published`, or terminal (4.3.6-4.3.7).

### 5.3 Location

- **Governorate** (required): dropdown from a fixed list of all 27 Egyptian governorates.
- **Area/landmark** (optional): free text up to 120 chars (e.g. "Nasr City, near City Stars"). Included in keyword search (4.4).
- No district dropdown, no interactive map or GPS pin in v1.

### 5.4 Reward flag

- Optional boolean + amount in **EGP** (whole numbers only).
- If the reward flag is true, the amount is required and must be an integer between **50 and 50,000**. If the flag is false, the amount must be empty.
- Shown on the public listing when set.
- Set at initial submission or on `Rejected` resubmit only - not editable in any other status (4.3.7).
- The platform never processes, holds, or escrows payment. Reward negotiation and exchange happen entirely offline.
- Demanding money beyond an advertised reward is prohibited in the Terms (7.4).

### 5.5 Admin moderation

- Every report must be approved before it becomes publicly visible. A single admin (the founder) manually reviews all submissions while volume is low.
- The approve / reject / resubmit lifecycle is defined in section 4.3.
- **Rejection reasons (predefined list):**
  1. Unclear photos
  2. Suspected spam or scam
  3. Duplicate report - the same reporter has posted the same item more than once
  4. Insufficient description
  5. Contains contact info
  6. Prohibited or illegal item
  7. Wrong category
  8. Contains a raw ID/document number in a text field
- An optional free-text note is shown to the reporter alongside the reason.
- Admin cannot edit report content - only approve, reject, and take enforcement actions (7.2).
- **Admin surfaces in v1:** moderation queue, abuse-report queue, user lookup (with ban and unban), and category & field management at `/admin/categories`. There is no admin tool for changing a user's phone number.
- **Category management:** admins may add categories (English `code`, sort order, `photosPrivate` flag, active flag), edit sort order, deactivate categories, and define per-category fields (`fieldKey`, type `text` or `integer`, validation ranges, required flag). Arabic labels for categories and fields are added in frontend translation files (`categories.json`) and deployed. Deactivated categories are hidden from new submissions; existing reports keep their category. Field-definition changes do not retroactively re-validate old reports. Categories referenced by reports cannot be deleted - only deactivated.
- **Admin access to private data:**
  - Private photos: during report review and enforcement investigations only.
  - Claim text and claim photos: during flagged-listing abuse or enforcement investigations only.
  - Chat threads: only when investigating a listing that has been flagged for abuse (7.1). Chat is not proactively monitored.
  - Hidden verification details: never (see rationale in 5.2).
- All moderation decisions (approve, reject, takedown, ban, unban) are recorded for audit, and those records survive deletion of the underlying report.

### 5.6 Messaging

- Real-time in-app chat (messages appear without a page refresh), unlocked only after a claim is approved.
- One thread per approved claim - not a general-purpose DM system.
- Text and photo attachments (same size and format rules as report photos).
- Chat messages are **not** subject to the contact-info block (4.1.3) - verified, matched parties may need to exchange contact details to coordinate a real-world handover.
- Safety banner on every new thread, linking to the safety page.
- **Report from chat:** see section 7.1 (shortcut to the listing-flag flow).
- Threads are listed in **My Chats** (4.8).
- After claim cancellation or report resolution the thread becomes read-only immediately and is deleted after 30 days (section 12).

### 5.7 Notifications

**Channels:**

- **In-app notification center** - the source of truth for all user-facing events. Notifications are not configurable; there is no preferences screen in v1.
- **SMS** - used only for OTP. Never used for event notifications.
- **Email** - used only to alert the admin that new submissions are waiting in the moderation queue. Never used for user-facing events.

**Read behavior:** notifications stay unread until the user opens or explicitly marks them read.

**Events that trigger an in-app notification:**

| Event                                     | Recipient                          | What it communicates                                                                                                   |
| ----------------------------------------- | ---------------------------------- | ---------------------------------------------------------------------------------------------------------------------- |
| Report approved                           | Reporter                           | Their report is now publicly visible; links to the report                                                              |
| Report rejected                           | Reporter                           | The reason and note; links to the rejected report, where it can be fixed and resubmitted                               |
| New claim submitted                       | Reporter                           | A claimant described the item; action required; links to the report's Claims section                                   |
| Claim withdrawn by claimant               | Reporter                           | A pending claim no longer needs review                                                                                 |
| Claim auto-withdrawn (reporter timeout)   | Reporter and claimant              | A pending claim was closed after 10 days without reporter action; no attempt consumed; report stays `Published`        |
| Report expiring soon                      | Reporter                           | Their published listing will auto-withdraw in 7 days unless withdrawn manually                                         |
| Report expired                            | Reporter and pending claimants     | The listing was auto-withdrawn after 90 cumulative published days; pending claims closed                               |
| Claim approved                            | Claimant                           | Their claim was accepted; chat is now available; links to the chat thread                                              |
| Claim rejected                            | Claimant                           | Their claim was rejected, including the system reason `Another claim approved`; links to the report if attempts remain |
| Claim closed - report no longer available | Claimant                           | The report was withdrawn, expired, or taken down, so their pending claim was closed; no attempt was consumed           |
| Claim cancelled by counterparty           | Other party                        | The approved claim was cancelled and the report is claimable again                                                     |
| Counterparty confirmed resolution         | Other party                        | The other party confirmed the item was returned; their own confirmation is awaited                                     |
| Report resolved                           | Both parties                       | Both confirmations were recorded and the report is closed                                                              |
| New chat message                          | Recipient                          | A new message in an active chat; suppressed while the recipient is viewing that same thread; links to the thread       |
| Admin takedown affecting you              | Reporter and any approved claimant | A report they are involved in was taken down                                                                           |
| Claim ended by enforcement                | Affected party                     | Their counterparty was banned, so the claim was cancelled                                                              |
| Abuse report resolved                     | Flagger                            | High-level outcome only (action taken or no action taken)                                                              |

### 5.8 Legal & privacy

- Basic Terms of Service and Privacy Policy - reasonable diligence for v1 under Egypt's Personal Data Protection Law context.
- **PDPL cross-border transfer:** infrastructure may be hosted outside Egypt. Accepted as a known legal gap for v1; hosting location disclosed in the Privacy Policy. Full legal review deferred.
- **PDPL data rights:** self-serve deletion is implemented (5.1). Correction and access rights are not: display names cannot be changed, phone numbers cannot be changed, and there is no data export in v1. This gap is accepted and disclosed in the Privacy Policy alongside the hosting disclosure.
- Minimal PII retention (section 12). Private photos and hidden verification details are access-controlled as defined in sections 5.2 and 9.

---

## 6. Verification & Claim Model

### 6.1 Who can claim

- Any logged-in user except the report's own reporter.
- Claiming is possible only while the report's status is `Published`. A claim attempt on any other status is refused and does not consume an attempt.
- A user may hold at most **one open `Pending` claim per report**, and may have active claims on multiple different reports.

### 6.2 Claim submission

- The claimant writes a free-text description, **10-500 characters**, prompted according to the report's direction:
  - On a **lost** report (posted by the owner), the claimant is the finder: _"Describe the item you found."_
  - On a **found** report (posted by the finder), the claimant is the owner: _"Describe the item you lost and anything that proves it is yours."_
- The claimant may attach **one optional photo** (same size and format rules as report photos). It is visible only to the reporter and, during an abuse or enforcement investigation, the admin.
- Claim text is normalized (trim + collapse repeated spaces) and is covered by the contact-info block (4.1.3).
- **Daily claim quota:** max **5 claim submissions per day** per account across all reports (Africa/Cairo day boundary).

### 6.3 Manual review by reporter

- Each claim submission creates a `Pending` claim record and notifies the reporter.
- The reporter approves or rejects manually - there is no automatic matching.
- **Reporter timeout:** a `Pending` claim is **auto-withdrawn** after **10 days** from `submittedAt` without reporter action. Status → `Withdrawn`; no attempt consumed; report stays `Published`. Reporter and claimant are notified in-app (`ClaimAutoWithdrawn`).
- **Claimant withdrawal:** the claimant may withdraw their own `Pending` claim at any time before the reporter decides. The claim becomes `Withdrawn`, no attempt is consumed, and the reporter is notified.

### 6.4 Claim statuses and attempt limits

Claim statuses: `Pending`, `Approved`, `Rejected`, `Withdrawn` (claimant withdrew or reporter-timeout auto-withdraw before a decision), `Cancelled` (an approved claim was ended by either party before resolution).

- **3 attempts** per user per report. After the 3rd counted failure that user is permanently blocked from claiming that report and is told so.
- **Counted as a failure:**
  - A manual rejection by the reporter.
  - Cancellation of an approved claim **by the claimant** (6.7).
- **Not counted:**
  - Cancellation of an approved claim by the reporter.
  - Voluntary withdrawal of a `Pending` claim by the claimant.
  - Auto-withdrawal of a `Pending` claim after the 10-day reporter timeout (6.3).
  - Auto-rejection because another claim was approved.
  - Auto-closure because the report is no longer available.
  - A claim refused because the report was not `Published`.
- No cooldown-and-retry cycles and no cross-report tracking in v1 - the limit is per user per report only.

### 6.5 Approval and locking

When the reporter approves a claim:

- The claim becomes `Approved` and the **report** moves to `Claim In Progress`.
- A chat thread is created for the two parties.
- All other `Pending` claims on that report are auto-rejected with the system reason `Another claim approved`, and those claimants are notified. No attempt is consumed.
- The listing stays publicly visible and searchable, labelled as having a claim in progress, but no new claims are accepted until the claim is cancelled or the report is resolved.

### 6.6 Rejection

- On manual rejection the claimant is notified and gets no chat access.
- The claimant may submit a new attempt if attempts remain (6.4).
- Rejection is final once attempts are exhausted - there is no admin appeal in v1. A claimant who believes they were wrongly rejected may use the support channel (section 3); the admin has no in-product tool to overturn a reporter's decision.

### 6.7 Cancellation and closure of claims

- **Cancellation of an approved claim:** either party may cancel while they have not themselves confirmed resolution (4.6.4). The claim becomes `Cancelled`, the report returns to `Published` and is claimable again, the counterparty is notified, and the chat thread becomes read-only immediately.
- **Closure of pending claims:** when a report leaves `Published` for any reason other than a claim approval - reporter withdrawal, **automatic expiry** (4.7), admin takedown, or ban and account-deletion cleanup - all `Pending` claims on it are closed automatically. Those claimants are notified, and no attempt is consumed.

---

## 7. Abuse, Safety & Enforcement

### 7.1 User-facing reporting

- **Report a listing:** any logged-in user except the listing owner can flag a publicly visible report (`Published` or `Claim In Progress`).
- **Reasons (predefined):** Scam/Fraud, Spam, Illegal/Prohibited item, Harassment/Threat, Other. An optional free-text note may be included.
- One open flag per user per listing. A duplicate flag on the same listing is refused with a clear message; existing flags cannot be edited.
- Flagged listings remain visible until the admin explicitly takes action.
- **Report from chat:** a **Report** control in the chat header (5.6) opens the same listing-flag flow as browse for the report linked to that thread. If the user already has an open flag on that listing, the UI shows the existing flag instead of creating a duplicate.
- **Block user** is out of scope in v1 (section 10). If a chat counterparty is abusive, the affected user flags the associated listing (from chat or browse) or uses the support channel (3).

### 7.2 Admin enforcement actions

- **Take down a report** → status becomes `Removed by Admin`. Any `Pending` claims are closed (6.7). If the report was `Claim In Progress`, the approved claim is cancelled first and its chat becomes read-only.
- **Ban a user** (reason recorded). Ban side effects:
  - Sign the user out of all devices immediately, and block future sign-in with the ban reason shown (5.1).
  - Withdraw their reports in `Pending Review` and `Published`, closing any pending claims on them.
  - Withdraw their `Pending` claims on other reports.
  - Cancel any `Approved` claim they are part of, whether as reporter or claimant; the chat becomes read-only. If the banned user was the reporter, the report is then withdrawn, so no report belonging to a banned user stays publicly visible.
  - Notify impacted counterparties.
- **Unban a user** - restores their ability to sign in and use the product. It does not restore anything that ban cleanup withdrew or cancelled.
- When investigating a flagged listing, the admin may access the associated chat content, claim text, and claim photos (5.5).
- **Abuse report workflow:** `Open` → `Resolved`, with an outcome of no action taken, report taken down, or user banned. The flagger is notified of the high-level outcome only.

### 7.3 Safety content

- Persistent banner at the top of every new chat thread: meet in public, prefer police stations, do not pay in advance.
- Static safety page linked from the footer and from the chat banner.

### 7.4 Extortion and fraud

- Demanding payment beyond an advertised reward is prohibited in the Terms.
- A user can flag the associated listing (7.1) or use the support channel (section 3); either is grounds for admin action up to a ban.
- The platform does not mediate payments or disputes over reward amounts.

### 7.5 Quotas, rate limits, and bot protection

- **Daily quotas:** 3 new reports per account per day (4.1.6); 5 claim submissions per account per day (6.2). Both use the Africa/Cairo day boundary. **Concurrent open-report cap:** max 5 in `Pending Review`, `Published`, or `Claim In Progress` per account (4.1.7). These caps make additional per-minute limits on the same two actions redundant, so none are defined.
- **Per-account rate limits:** photo uploads 5 per minute and 20 per hour; chat messages 10 per minute and 60 per hour.
- When a limit is exceeded the user sees a clear try-again-later message.
- OTP send limits and the bot check are defined in section 5.1.

---

## 8. Report Lifecycle & Status Transitions

### 8.1 Status list

| Status              | Description                                                                                                                                                                         |
| ------------------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `Pending Review`    | Submitted, awaiting admin approval. Not publicly visible and not editable                                                                                                           |
| `Rejected`          | Admin rejected it, with a reason and optional note. Readable only by its reporter and the admin; editable and resubmittable up to 3 times; deleted after 30 days if not resubmitted |
| `Published`         | Approved, publicly visible and claimable                                                                                                                                            |
| `Claim In Progress` | A claim was approved; chat active; publicly visible and labelled but not claimable                                                                                                  |
| `Resolved`          | Closed and final after both parties confirmed                                                                                                                                       |
| `Withdrawn`         | Withdrawn by the reporter, by ban/account-deletion cleanup, or by automatic expiry (`_expired_`)                                                                                    |
| `Removed by Admin`  | Taken down by the admin                                                                                                                                                             |

### 8.2 Transition table

| From                | To                  | Triggered by                                                                  | Notes                                                                                                            |
| ------------------- | ------------------- | ----------------------------------------------------------------------------- | ---------------------------------------------------------------------------------------------------------------- |
| -                   | `Pending Review`    | User submits report                                                           | Counts against the daily submission quota                                                                        |
| `Pending Review`    | `Published`         | Admin approves                                                                |                                                                                                                  |
| `Pending Review`    | `Rejected`          | Admin rejects                                                                 | Reason + optional note; reporter notified                                                                        |
| `Pending Review`    | `Withdrawn`         | Reporter withdraws before a decision                                          |                                                                                                                  |
| `Rejected`          | `Pending Review`    | Reporter fixes and resubmits                                                  | Max 3 resubmissions; does not count against the daily quota; photo visibility re-derived if the category changed |
| `Published`         | `Claim In Progress` | Reporter approves a claim                                                     | Locks new claims; chat created; other pending claims auto-rejected                                               |
| `Published`         | `Withdrawn`         | Reporter withdraws, automatic expiry (4.7), or ban / account-deletion cleanup | Pending claims closed (6.7)                                                                                      |
| `Published`         | `Removed by Admin`  | Admin takedown                                                                | Pending claims closed (6.7)                                                                                      |
| `Claim In Progress` | `Published`         | Either party cancels the claim                                                | Claim becomes `Cancelled`; chat read-only                                                                        |
| `Claim In Progress` | `Resolved`          | Both parties confirm                                                          | Final                                                                                                            |
| `Claim In Progress` | `Withdrawn`         | Ban cleanup where the banned user is the reporter                             | Approved claim cancelled first                                                                                   |
| `Claim In Progress` | `Removed by Admin`  | Admin takedown                                                                | Approved claim cancelled first; chat read-only                                                                   |

`Resolved`, `Withdrawn`, and `Removed by Admin` are terminal - no reopening or reactivation in v1. `Rejected` is terminal only in the sense that it is not publicly visible; it leaves that state solely by resubmission, and is deleted once its retention window expires.

---

## 9. Permissions Matrix

What each role can see for a given report:

| Data                                        | Public visitor | Logged-in user | Pending claimant | Approved claimant | Reporter       | Admin                                  |
| ------------------------------------------- | -------------- | -------------- | ---------------- | ----------------- | -------------- | -------------------------------------- |
| Title, description, category fields         | ✓              | ✓              | ✓                | ✓                 | ✓              | ✓                                      |
| Public photos                               | ✓              | ✓              | ✓                | ✓                 | ✓              | ✓                                      |
| Private photos (`photosPrivate` categories) |                |                |                  |                   | ✓              | ✓ (review/enforcement only)            |
| Hidden verification detail                  |                |                |                  |                   | ✓              |                                        |
| Reward amount, item-held location           | ✓              | ✓              | ✓                | ✓                 | ✓              | ✓                                      |
| Display name of reporter                    | ✓              | ✓              | ✓                | ✓                 | own            | ✓                                      |
| Display name of claimant                    |                |                | own              | own               | ✓              | ✓                                      |
| Phone numbers                               |                |                | own              | own               | own            | ✓                                      |
| Claim text and claim photo                  |                |                | own              | own               | ✓ (for review) | ✓ (flagged-listing investigation only) |
| Chat thread                                 |                |                |                  | ✓                 | ✓              | ✓ (flagged-listing investigation only) |
| Withdrawal reason                           |                |                |                  |                   | own            | ✓                                      |

Per-field access above. Public visibility by status: section 4.4.

---

## 10. Explicitly Out of Scope for v1

- **Automated/AI-assisted matching** of any kind, including identifier-based pairing (IMEI, serial).
- **Claim rejection appeals** - a rejection is final once attempts are exhausted.
- **Editing a published report's content** - the reporter withdraws and submits a new report instead (4.3.7).
- **In-chat block user** - listing-level reporting (including from chat) exists in v1; per-user blocking does not.
- **Per-photo visibility control** - photo visibility is category-driven only.
- **Resolution disputes/reopening** - confirmations are final and irrevocable.
- **Display-name edits after signup.**
- **Phone-number change** - neither self-serve nor admin-assisted.
- **Terms re-acceptance flow** when the Terms version changes.
- **Notification preferences** - all notifications are always on.
- **Data export / access requests** (5.8).
- **Automated profanity filtering** - moderation and abuse reporting only.
- **Social link previews and rich share cards** - shared links show a plain URL.
- **In-app payments/escrow** for rewards - offline negotiation only.
- **Native mobile apps** - web app only (installable on home screen).
- **English end-user UI** - Arabic UI only for v1; catalog data uses English keys in DB with Arabic labels in frontend i18n.
- **Community-assisted moderation** - solo-admin review only.
- **Monetization** (ads, premium boosts, donations) - fully free in v1.
- **Web push notifications** - the in-app notification center is the only user-facing event channel.
- **SMS notifications for in-app events** - SMS is used solely for OTP.
- **Map/GPS-based location picking** - governorate dropdown + free-text area only.
- **Draft report saving** - single-session submission only.
- **Formal performance targets** - no page-load or bundle-size budgets for v1.

---

## 11. Non-Functional Requirements

### 11.1 Browser support

Last 2 major versions of Chrome, Safari, Firefox, and Edge; Android WebView 90+; iOS Safari 15+.

### 11.2 Accessibility and presentation

- Target: **WCAG 2.1 AA** as a goal (not formally audited in v1).
- Keyboard navigation and screen-reader sanity on core flows (browse, submit, claim, chat).
- Dates and times are displayed in the **Gregorian** calendar with **Western Arabic numerals** (123, not ١٢٣), in **Africa/Cairo** time.

### 11.3 Performance

No formal performance targets for v1. Optimized image thumbnails are used to keep page weight reasonable on mobile networks.

---

## 12. Data Retention

- Verification codes: discarded within **24 hours** after expiry.
- Session credentials: discarded within **30 days** after expiry or sign-out.
- **Report photos:** retained while `Pending Review`, `Rejected`, `Published`, or `Claim In Progress`; deleted immediately when the report becomes `Resolved`, `Withdrawn`, or `Removed by Admin`.
- **Rejected reports:** deleted **30 days** after rejection if never resubmitted. Resubmission restarts the report's life; a later rejection starts a new 30-day window.
- **Claim photos:** deleted when the claim reaches `Rejected`, `Withdrawn`, or `Cancelled`, or when the report reaches a terminal status.
- **Chat threads and messages:** read-only immediately on claim cancellation or report resolution; permanently deleted **30 days** later.
- **On account deletion:** user signed out immediately; message bodies remain until the normal chat deadline while sender identity is anonymized immediately; direct personal data purged within **30 days**; anonymized aggregates retained only where required for audit.
- **Retained internally:** moderation decisions and audit metadata. Moderation records survive deletion of the report they refer to.

Entity-level schedule (implementation): `OtpCode`, `RefreshToken`, `Report`, `ReportPhoto`, `Claim`, chat records, and `ModerationAction` follow the rules above; scheduled jobs enforce retention windows, **listing expiry** (4.7), and **pending-claim timeout** (6.3).

---

## 13. Risks & Mitigations

| Risk                                                            | Mitigation                                                                                                                                                              |
| --------------------------------------------------------------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| False claims / theft of found items                             | Manual claim review before chat unlocks + required hidden verification detail + optional claim photo (one max - keeps reporter review lightweight)                      |
| False negatives (real owner rejected)                           | Up to 3 attempts per user/report; accepted no-appeal trade-off in v1                                                                                                    |
| Weak hidden verification details                                | Reporter's own interest plus a minimum length; no longer admin-reviewed, accepted in exchange for the reporter's privacy                                                |
| Reporter never reviews claims                                   | Action-required in-app notifications + claim visibility in My Reports; **10-day auto-withdraw** of pending claims (6.3)                                                 |
| Reunions never confirmed, so the primary metric undercounts     | Confirmation prompts in chat + counterparty-confirmed notification + optional withdrawal reasons (4.7)                                                                  |
| Spam / fake accounts                                            | Phone OTP + bot check + admin review + daily quotas                                                                                                                     |
| OTP cost / OTP bombing                                          | Bot check + per-phone send limits (5.1) + cost monitoring; SMS provider TBD (section 14)                                                                                |
| PII exposure (especially IDs)                                   | `photosPrivate` category photos private to reporter and admin only; no raw ID numbers; first name only, never a full name; photo metadata removed on upload             |
| Contact info bypassing verification                             | Users posting phones/links in public fields to skip claim flow; mitigated by contact-pattern block (4.1.3) + admin rejection reason 5; chat exempt after claim approved |
| Harassment / extortion via chat                                 | Listing-level abuse reporting (including from chat), safety guidance, anti-extortion Terms, admin ban capability, published support channel                             |
| Stale published listings                                        | 90 cumulative published days then auto-withdraw (4.7); 7-day warning notification                                                                                       |
| Moderation bottleneck                                           | Single-admin model accepted at low volume; email alerts on new submissions; monitor queue latency                                                                       |
| Thin nationwide density                                         | Accepted at low traffic; monitor by governorate and revisit seeding strategy if needed                                                                                  |
| PDPL cross-border transfer and missing correction/access rights | Disclosed in the Privacy Policy as accepted v1 gaps pending formal review                                                                                               |
| Limited operational visibility                                  | Accepted v1 trade-off; monitor via hosting platform logs                                                                                                                |
| Weak discovery with simple keyword search                       | Keyword search over title, description, category fields and area, with Arabic normalization                                                                             |
| No link previews on shared URLs                                 | Accepted trade-off for v1                                                                                                                                               |

---

## 14. Deferred Decisions

| Item                                          | Status                                                                                                               |
| --------------------------------------------- | -------------------------------------------------------------------------------------------------------------------- |
| OTP / SMS provider                            | **Partial** - `ISmsSender` defined in [Phase 01](./PHASE-01-platform-foundation.md); vendor chosen at production deploy |
| API error contract appendix                   | **Resolved** - [api-error-contract.md](./api-error-contract.md)                                                      |
| SignalR event/payload contract                | **Pending** before implementation                                                                                    |
| Transactional email provider for admin alerts | **To be chosen** before launch                                                                                       |
| Domain name                                   | **To be chosen** before launch                                                                                       |

---

## 15. Acceptance Criteria (v1 core flows)

Verification checkpoints for Part I. Where a flow is fully defined above, the criterion states the expected outcome only.

### 15.1 Report submission and validation

- **Valid submission creates a pending report:** given a logged-in user with remaining quota, when they submit all required fields including the hidden verification detail, then a report is created with status `Pending Review`.
- **Date bounds:** a date lost/found in the future, or more than 12 months before today in Africa/Cairo time, is rejected with field-level validation. Today's local date is always accepted.
- **Hidden-detail format:** the hidden verification detail is private text of 10-500 characters, and is required.
- **Contact info is blocked in scoped fields:** URL/social-domain text or a phone-like sequence of 10+ digits after normalization is rejected with field-level validation in title, description, area, held-location detail, public category fields, and claim text - and is accepted in the hidden verification detail and in chat messages.
- **Category fields:** required category fields are validated per the active category's field definitions (5.2), including seed defaults (text 2-80 chars, `first name on document` 2-40 letters/spaces, `key count` integer 1-20).
- **Submission quota:** at 3 new reports in the current Africa/Cairo day, the next submission is rejected with clear quota messaging.
- **Open-report cap:** at 5 reports in `Pending Review`, `Published`, or `Claim In Progress`, the next new submission is rejected with clear cap messaging; resubmitting a `Rejected` report still succeeds.

### 15.2 Moderation, rejection, and resubmission

- **Approve flow:** approving a `Pending Review` report sets it to `Published` and it appears in public listings.
- **Reject flow:** rejecting sets the status to `Rejected` with the chosen reason and optional note, notifies the reporter, keeps the report and photos, and removes it from the moderation queue. The report is readable by its reporter and the admin, and its URL shows a not-found page to anyone else.
- **Fix and resubmit:** editing and resubmitting a `Rejected` report sets it to `Pending Review`, does not consume the daily submission quota, and re-runs the contact-info block.
- **Resubmission cap:** after the 3rd resubmission is rejected, further resubmission of that report is refused with a clear message.
- **Category change on resubmission:** changing a report's category to one with `photosPrivate` makes its existing photos private, and changing to one without makes them public.
- **No editing outside `Rejected`:** content edit attempts are refused while the report is `Pending Review`, `Published`, `Claim In Progress`, or terminal (including reward flag/amount).
- **Rejected retention:** a `Rejected` report and its photos are deleted 30 days after rejection when never resubmitted; the moderation decision record survives.
- **Listing expiry warning:** when a `Published` report has **83 cumulative published days** elapsed (7 days before auto-expiry), the reporter receives `ReportExpiringSoon`.
- **Listing auto-expiry:** after **90 cumulative days** in `Published` (timer paused during `Claim In Progress`), the report becomes `Withdrawn` with `_expired_`; pending claims close; reporter and claimants are notified.
- **No expiry while in review:** `Pending Review` and `Rejected` reports never auto-expire.

### 15.3 Browse, search, visibility, and URLs

- **Listing scope:** browse and filter requests return `Published` and `Claim In Progress` reports and nothing else.
- **Claim In Progress presentation:** a `Claim In Progress` report is publicly readable, labelled as having a claim in progress, and its claim action is unavailable.
- **Search behavior:** a query matches only reports where every query word appears, in any order and case-insensitively, across title, description, public category field values, and area text, after Arabic normalization - so a query using a bare alef, a haa in place of taa marbuta, tatweel, or no diacritics still matches the equivalent stored text.
- **Filters:** category, governorate, type, and date range combine with the keyword query using AND.
- **URL behavior:** `Resolved`, `Withdrawn`, and `Removed by Admin` show a permanently-unavailable page; `Pending Review` and `Rejected` show a not-found page to everyone but their reporter and the admin; missing IDs and wrong-type links show a not-found page.

### 15.4 Claiming and review

- **Claim creation constraints:** a logged-in non-reporter submitting 10-500 characters on a `Published` report creates a `Pending` claim. Claim creation is refused on any other status without consuming an attempt, and refused when that user already has an open `Pending` claim on the report.
- **Direction-specific prompt:** the claim form asks the finder to describe the item they found on a lost report, and asks the owner to describe and prove the item on a found report.
- **Claim photo:** at most one photo may be attached, and it is visible only to the reporter and, during a flagged-listing investigation, the admin.
- **Reporter timeout:** a `Pending` claim is auto-withdrawn after **10 days** without reporter action; no attempt is consumed; the report stays `Published`; both parties are notified.
- **Claimant withdrawal:** withdrawing a pending claim sets it to `Withdrawn`, consumes no attempt, and notifies the reporter.
- **Approval side effects:** the claim becomes `Approved`, the report becomes `Claim In Progress`, a chat thread is created, and all other pending claims become `Rejected` with the reason `Another claim approved`, notified, with no attempt consumed.
- **Attempt counting:** manual reporter rejections and claimant-initiated cancellations of an approved claim each consume one attempt; reporter-initiated cancellations, claimant withdrawals, **10-day auto-withdrawals**, auto-rejections, auto-closures, and refused claims do not. After 3 counted failures, further claims on that report by that user are blocked with a clear message.
- **Daily claim quota:** at 5 claim submissions in the current Africa/Cairo day, the next claim is rejected with clear quota messaging.
- **Pending claim closure:** when a report is withdrawn, expired, or taken down, its pending claims are closed automatically, those claimants are notified, and no attempt is consumed.

### 15.5 Resolution and chat

- **Mutual confirmation is the only resolve path:** the report becomes `Resolved` only when both parties have confirmed. Reporter-only close and one-sided timeout resolution do not exist.
- **Confirmation is irrevocable:** a party who has confirmed cannot un-confirm and cannot cancel the claim; the other party can still confirm or cancel.
- **Confirmation notifications:** the first confirmation notifies the counterparty that their confirmation is awaited, and the second notifies both parties that the report is resolved.
- **Cancellation path:** cancelling before mutual confirmation sets the claim to `Cancelled`, returns the report to `Published`, notifies the counterparty, and makes the chat read-only immediately.
- **Chat retention:** after cancellation or resolution the thread is read-only and is deleted 30 days later.
- **Chat reachability:** both parties can still open a read-only thread from My Chats while it exists, even though the report's public URL is unavailable.

### 15.6 Abuse and enforcement

- **Flagging constraints:** a listing owner cannot flag their own listing, each user can have at most one open flag per listing, a duplicate flag is refused, and the reason must come from the predefined list.
- **Abuse workflow:** an abuse report moves `Open` → `Resolved` with an outcome of no action taken, report taken down, or user banned, and the flagger is notified of the high-level outcome.
- **Ban cleanup:** on ban the user is signed out everywhere, their `Pending Review` and `Published` reports are withdrawn, their pending claims are withdrawn, any approved claim they are part of is cancelled, a report of theirs in `Claim In Progress` ends as `Withdrawn`, impacted counterparties are notified, and a later sign-in attempt is refused with the ban reason.
- **Unban:** an unbanned user can sign in again, and nothing withdrawn or cancelled by the ban is restored.
- **Takedown during `Claim In Progress`:** the approved claim is cancelled first, the report becomes `Removed by Admin`, and the chat becomes read-only immediately.
- **In-chat report shortcut:** a user in a chat thread can open the listing-flag flow for the linked report; if they already have an open flag, the UI shows that flag instead of creating a duplicate.

### 15.7 Authentication (OTP)

- **Successful send:** given a passed bot check and a phone under the send limits, an SMS is sent and the user can complete signup or login with the code.
- **Resend cooldown:** a resend requested less than **120 seconds** after the last send is blocked with a clear wait message and no SMS is sent.
- **Hourly send limit:** after **2** sends for the same phone in the rolling hour, further requests are blocked with a clear limit message and no SMS is sent.
- **Daily send limit:** after **3** sends for the same phone in the rolling day, further requests are blocked with a clear limit message and no SMS is sent.
- **Verification attempt limit:** after **3** failed entries the code is void and a new code must be requested.
- **Account creation point:** abandoning signup after OTP verification but before submitting a display name and accepting the Terms leaves no account, and the same phone can start signup again.
- **Banned sign-in:** a banned user's sign-in is refused with the recorded ban reason.
- **Provider outage:** when the verification service is unavailable, signup/login is blocked with a clear temporary-unavailable message and no access is granted.

### 15.8 Privacy and permissions

- **Private photos (`photosPrivate`):** categories with `photosPrivate` never expose photos on a public listing or to claimants - only the reporter and admin (during review/enforcement).
- **Hidden verification detail:** it is returned only to its reporter, and never to a claimant, the public, or the admin.
- **Claim text and photos:** visible to their own claimant and the reporter at all times, and to the admin only while a flagged-listing investigation is open.
- **Chat access:** only the two parties can read a thread, plus the admin during a flagged-listing investigation.
- **Phone numbers:** never returned to another user in any surface.
- **Role enforcement:** every row of the section 9 matrix is enforced server-side, not merely hidden in the UI.

### 15.9 Notifications

- Each event in the section 5.7 table produces exactly one in-app notification for each listed recipient, deep-linking to the relevant report, claim, or thread.
- A new-message notification is suppressed while the recipient is viewing that same thread.
- Notifications remain unread until opened or explicitly marked read, and no setting can disable any of them.
- SMS is sent only for OTP; email is sent only to the admin for pending submissions.

---

# Part II - Technical Specification

---

## 16. Architecture & Stack

- **Frontend:** Angular, built as an installable PWA, Arabic-first RTL layout. Plain client-side rendering - no SSR in v1.
- **Backend:** ASP.NET Core Web API, with SignalR for real-time chat (5.6).
- **Database:** PostgreSQL (EF Core + Npgsql). Hosted on **Supabase** in production (managed Postgres). Local dev uses native PostgreSQL on Windows; integration tests use PostgreSQL via Testcontainers.
- **Hosting:** **Render** Free Docker web service — serves Angular PWA and .NET API from one origin. See [deployment.md](./deployment.md).
- **Object storage:** **Cloudflare R2** (S3-compatible) - `public/` and `private/` prefixes for media.
- **Private media access:** private report photos (`photosPrivate` categories) and claim photos are served via short-lived **pre-signed URLs (5-minute expiry)**, generated per request after the access-control check in section 9. Authorized viewers: reporter and admin for report photos; claimant, reporter, and (during a flagged-listing investigation) admin for claim photos.
- **Expired private URL behavior:** if a private image URL expires while viewing, the client silently requests a fresh authorized URL and retries.
- **Timezone storage:** per section 3 - `timestamptz` in UTC; `date` fields as Cairo calendar days; quotas and validation use Africa/Cairo; display in Africa/Cairo (11.2).
- **Search implementation:** 4.4 requires all-terms matching with Arabic normalization, which plain `ILIKE` on raw columns cannot deliver.
  - Each report carries a denormalized, normalized search column built from title + description + public category field values + area text, recomputed whenever the report is written.
  - Normalization (applied identically to stored text and to the incoming query): alef variants (`أ إ آ ٱ` → `ا`), `ى` → `ي`, `ة` → `ه`, strip tatweel and Arabic diacritics, collapse whitespace, lowercase.
  - Matching: the query is normalized and split into terms; every term must match the search column (`ILIKE '%term%'`, AND-ed). A trigram index (`pg_trgm` GIN) on the search column keeps this workable.
  - No external search infrastructure. Postgres full-text search (`tsvector`) is a post-v1 upgrade.
- **Caching:** `HybridCache` (L1 in-process + L2 `MemoryDistributedCache`) behind `ICacheService` (v1, single API instance). Cache-aside for **catalog data only** (categories, governorates); explicit invalidation on admin category writes. Built-in stampede protection; fail-open to DB on cache errors. **Browse/search is not cached in v1.** **Do not cache:** OTP send limits (DB-backed), JWT/refresh tokens, pre-signed media URLs. Swap L2 to Redis when running multiple API instances.

  | Cache key              | Value                                                   | TTL (default) | Invalidation                   |
  | ---------------------- | ------------------------------------------------------- | ------------- | ------------------------------ |
  | `catalog:categories`   | Active categories + field defs (`CacheKeys.Categories`) | 1h            | Admin category CRUD (Phase 03) |
  | `catalog:governorates` | Governorate list (`CacheKeys.Governorates`)             | 24h           | Seed change (rare)             |

- **Admin dashboard:** same Angular app behind a role guard at `/admin/`. Screens match section 5.5.

---

## 17. Data Model

| Entity                    | Key fields                                                                                                                                                                                                                                                                                                                                |
| ------------------------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `User`                    | normalized phone (`+20...`), display name, role, banned flag + reason, ToS version + accepted-at, created-at                                                                                                                                                                                                                              |
| `Category`                | code (English key), sort order, `photosPrivate` flag, active flag                                                                                                                                                                                                                                                                         |
| `CategoryFieldDefinition` | category ref, field key (snake_case), type (`text`/`integer`), validation rules, required flag, sort order                                                                                                                                                                                                                                |
| `Governorate`             | code (English key), sort order                                                                                                                                                                                                                                                                                                            |
| `Report`                  | type (Lost/Found), category, title, description, date lost/found, governorate, area text, item-held location (found), status, reward flag/amount, hidden-detail text, withdrawal reason, resubmission count, normalized search text, published-at, published-seconds-elapsed, published-timer-resumed-at, expiry-warning-sent, timestamps |
| `CategoryField`           | report ref, field key, value                                                                                                                                                                                                                                                                                                              |
| `ReportPhoto`             | report ref, storage key, content type, size, thumbnail ref, sort order                                                                                                                                                                                                                                                                    |
| `Claim`                   | report ref, claimant user, status (`Pending`/`Approved`/`Rejected`/`Withdrawn`/`Cancelled`), submitted answer, optional photo, submitted-at, decision reason, reviewer decision, reviewed-at, cancelled-by, attempt number, counts-as-attempt flag                                                                                        |
| `Resolution`              | report ref, reporter-confirmed-at, claimant-confirmed-at, resolved-at                                                                                                                                                                                                                                                                     |
| `ChatThread`              | claim ref, created-at, read-only-at                                                                                                                                                                                                                                                                                                       |
| `Message`                 | thread ref, sender, body, attachment ref, timestamp                                                                                                                                                                                                                                                                                       |
| `Notification`            | user ref, type, payload (`type`, `createdAt`, `deepLink`, optional `reportId`/`claimId`/`chatThreadId`), read state, timestamp                                                                                                                                                                                                            |
| `OtpCode`                 | phone, code hash, expires-at, attempt count                                                                                                                                                                                                                                                                                               |
| `RefreshToken`            | user ref, token hash, expires-at, revoked flag                                                                                                                                                                                                                                                                                            |
| `AbuseReport`             | reporter user, report ref, reason enum, note, status (`Open`/`Resolved`), resolution outcome, resolved-by, resolved-at                                                                                                                                                                                                                    |
| `ModerationAction`        | report ref (nullable), admin ref, decision, reason, note, timestamp                                                                                                                                                                                                                                                                       |

Implementation notes:

- Categories and field definitions are seeded at deploy and admin-editable thereafter (section 5.2). Photo visibility is derived from the category's `photosPrivate` flag and re-derived on resubmission (section 4.3).
- Hidden verification detail is on `Report` and is never returned except to its reporter (sections 5.2 and 9).
- `ModerationAction` survives report deletion (section 12) with a nullable report reference.
- `Claim.counts-as-attempt` is persisted per claim (section 6.4).

---

## 18. Authentication & Sessions

Implementation for section 5.1:

- **OTP:** SMS delivery provider **deferred** (section 14) - implement behind an abstracted interface; concrete provider chosen before launch. v1 still requires SMS OTP per section 5.1.
- **Account creation:** `User` row created only when display name and Terms are submitted.
- **Session:** JWT access token (15 minutes) + refresh token (30 days), rotating on refresh. Multi-device allowed.
- **Logout everywhere:** revokes all refresh tokens; active access tokens expire within one access-token lifetime.
- **Ban enforcement:** checked on token issue and refresh.
- **Admin bootstrap:** admin phone seeded from environment variable at deploy.
- **Bot protection:** CAPTCHA before OTP send.

---

## 19. Uploads & Media Processing

- **Formats:** JPEG, PNG, WebP; max 5 MB per file (report photos, claim photo, chat attachments).
- **Processing:** EXIF stripped; server generates WebP thumbnails.
- **Private media:** pre-signed URLs (section 16).

---

## 20. API Contracts

### 20.1 Notifications

Maps to section 5.7 event types. Payload: required `type`, `createdAt`, `deepLink`; optional `reportId`, `claimId`, `chatThreadId`.

Type identifiers: `ReportApproved`, `ReportRejected`, `NewClaimSubmitted`, `ClaimWithdrawnByClaimant`, `ClaimAutoWithdrawn`, `ReportExpiringSoon`, `ReportExpired`, `ClaimApproved`, `ClaimRejected`, `ClaimClosedReportUnavailable`, `ClaimCancelledByCounterparty`, `CounterpartyConfirmedResolution`, `ReportResolved`, `NewChatMessage`, `AdminTakedownAffectingYou`, `ClaimEndedByEnforcement`, `AbuseReportResolvedForFlagger`.

### 20.2 Rate limiting

Per section 7.5. On limit exceed: HTTP `429` with `Retry-After` header.

---

## 21. Infrastructure & Operations

- **Environments:** production only (plus local dev on developer machines).
- **Database:** PostgreSQL everywhere.
  - **Local dev:** native PostgreSQL 16+ on Windows (`localhost:5432`; connection string in `api/appsettings.Development.json`).
  - **Integration tests:** PostgreSQL 16 via Testcontainers (`postgres:16`); `IClassFixture` web application factories override `ConnectionStrings:Default`. Requires Docker; no docker-compose in repo.
  - **Production:** Supabase managed PostgreSQL (direct connection, SSL).
- **Migrations:** EF Core migrations on API startup.
- **Scheduled jobs:** retention cleanup (section 12); **listing expiry** and expiry-warning checks (4.7); **pending-claim timeout** (6.3). All business-date logic uses Africa/Cairo day boundaries where applicable.
- **Backups:** Supabase managed Postgres defaults.
- **Monitoring:** Render logs.
- **Caching:** `HybridCache` via `ICacheService` (Section 16). Config: `Cache:CategoriesTtlSeconds`, `Cache:GovernoratesTtlSeconds` in `appsettings.json`. L2 is memory in v1; Redis when multi-instance.
- **Transactional email:** admin moderation-queue alert only (section 5.7). Provider: section 14.
- **Budget:** ~$0/month infra for MVP testing (Render + Supabase free tiers); ~$5/month recommended before public launch for always-on API. SMS cost depends on chosen provider (section 14).
- **Domain:** section 14.
- **Hosting:** Render (API + static frontend) + Supabase Postgres + Cloudflare R2, outside Egypt (section 5.8). See [deployment.md](./deployment.md).
