# Sub-phase 03 — Claim Photo Upload

**Status:** Not started  
**Prerequisites:** Phase 02 [Sub-phase 04 — Photo Upload](../phase-02/SUBPHASE-02-04-photo-upload.md)

---

## 1. Summary

Implement private claim photo upload: `POST /api/uploads/claim-photo` and pre-signed URL retrieval. Reuses image pipeline from report photos; always private bucket. Max 1 photo per claim (enforced at claim create in sub-phase 04).

---

## 2. SPEC references

| SPEC section | Topic |
| ------------ | ----- |
| Section 6.2 | Optional claim photo, private |
| Section 9 | Claimant + reporter + admin (investigation) access |
| Section 19 | Upload rules (5 MB, JPEG/PNG/WebP, EXIF strip) |

---

## 3. What you will learn

- Extending upload pattern for a new media type
- Orphan claim photos before claim link (same TTL pattern as report photos)
- Access control: reporter + claimant + admin (investigation stub returns 404 until Phase 08)

**Files to read after implementing:**

- `api/Controllers/UploadsController.cs` (claim-photo actions)
- `api/Services/Media/ClaimPhotoUploadService.cs`
- `api/Data/Entities/Claim.cs` — `photoId` nullable FK

---

## 4. Deliverables

### Endpoints

| Method | Route | Auth |
| ------ | ----- | ---- |
| POST | `/api/uploads/claim-photo` | Required |
| GET | `/api/uploads/claim-photo/{id}/url` | Required |

### Upload rules

- Same format/size/EXIF strip as report photos (Phase 02)
- Always **private** bucket prefix
- Rate limits: **shared** `photo uploads` bucket with report photos — 5/min and 20/hour per account (SPEC 7.5 aggregate; do not use a separate counter)

### URL access

| Viewer | Access |
| ------ | ------ |
| Uploader (claimant) | ✓ |
| Report reporter | ✓ (when claim linked) |
| Admin | ✓ (Phase 08 investigation — return 404 stub OK with TODO) |
| Others | `404` |

---

## 5. Step-by-step implementation order

1. Add `ClaimPhoto` storage entity or reuse `ReportPhoto`-like table
2. Implement upload service (private only)
3. Add controller actions
4. Integration tests: upload, presigned URL, access denied for stranger
5. Manual test with Swagger

---

## 6. Out of scope

- Linking photo to claim (sub-phase 04)
- Chat attachments (Phase 06)

---

## 7. Validation gate

- [ ] Upload → private bucket key
- [ ] EXIF stripped
- [ ] Stranger cannot get URL
- [ ] Reporter can get URL when claim exists linking photo

---

## 8. Exit criteria

- [ ] Claim photo upload API tested
- [ ] Mark sub-phase 03 complete in [phase-05/README.md](./README.md)
