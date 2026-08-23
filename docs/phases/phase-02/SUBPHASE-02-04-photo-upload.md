# Sub-phase 04 — Photo Upload API

**Status:** Not started  
**Prerequisites:** [Sub-phase 01 — Reference Data](./SUBPHASE-02-01-reference-data.md), Phase 01 auth ([Sub-phase 08](../phase-01/SUBPHASE-01-08-sessions.md))

---

## 1. Summary

Implement authenticated report photo upload with EXIF stripping, WebP thumbnail generation, public/private bucket routing based on category `photosPrivate`, upload rate limits, and pre-signed URL retrieval. Photos are uploaded **before** report creation; sub-phase 05 links them by ID.

---

## 2. SPEC references

| SPEC section | Topic |
| ------------ | ----- |
| Section 4.1 | 0–5 photos, 5 MB each |
| Section 5.2 | `photosPrivate` category routing |
| Section 7.5 | Upload rate limits (5/min, 20/hour) |
| Section 9 | Private photo access (reporter + admin) |
| Section 16 | Pre-signed URLs (5-minute expiry) |
| Section 19 | JPEG/PNG/WebP, EXIF strip, WebP thumbnails |

---

## 3. What you will learn

- Multipart file upload in ASP.NET Core with size limits
- Image processing pipeline (EXIF removal, resize, WebP encode) — e.g. `SixLabors.ImageSharp`
- S3-compatible storage with Railway Buckets (public vs private prefix)
- Orphan photo handling: uploaded photos not yet linked to a report expire after a TTL (e.g. 24h cleanup job stub OK)
- Pre-signed URL generation with 5-minute expiry

**Files to read after implementing:**

- `api/Controllers/UploadsController.cs`
- `api/Services/Media/IImageProcessor.cs`, `ImageProcessor.cs`
- `api/Services/Media/IObjectStorage.cs`, `S3ObjectStorage.cs`
- `api/Services/Media/ReportPhotoUploadService.cs`
- `api/Data/Entities/ReportPhoto.cs` (may add `UploadedByUserId`, `ExpiresAt` for orphans)
- `api.Tests/Controllers/UploadsControllerTests.cs`

---

## 4. Deliverables

### Endpoints

| Method | Route | Auth | Purpose |
| ------ | ----- | ---- | ------- |
| POST | `/api/uploads/report-photo` | Required | Upload one photo |
| GET | `/api/uploads/report-photo/{id}/url` | Required | Pre-signed URL (private) or public URL |

### `POST /api/uploads/report-photo`

**Request:** `multipart/form-data`

| Field | Required | Rules |
| ----- | -------- | ----- |
| `file` | Yes | JPEG, PNG, or WebP; max 5 MB |
| `categoryId` | Yes | Determines public vs private bucket prefix |

**Flow:**

1. Authenticate user
2. Check upload rate limits (5/min, 20/hour per user) → `429` + `Retry-After`
3. Validate content type and size
4. Load category; reject if inactive
5. Strip EXIF; generate WebP thumbnail (max dimension e.g. 400px)
6. Store original + thumbnail in bucket:
   - `photosPrivate = false` → public prefix
   - `photosPrivate = true` → private prefix
7. Create `ReportPhoto` row (no `ReportId` yet — orphan until linked in sub-phase 05)
8. Return `{ id, thumbnailUrl }` — for public photos, `thumbnailUrl` may be direct; for private, a short-lived presigned URL

### `GET /api/uploads/report-photo/{id}/url`

- **Public photo:** return stable public URL (or redirect)
- **Private photo:** generate 5-minute pre-signed URL
- **Access:** reporter who uploaded OR admin during **moderation review** (`Pending Review` report). Others → `404`
- After report is linked: reporter (owner) OR admin (same review gate)
- **Enforcement investigation:** private report photos for flagged listings use Phase 08 `GET /api/admin/investigations/{reportId}/photos` (open abuse flag required) — not this endpoint

### Rate limits (Section 7.5)

| Window | Limit | Error code |
| ------ | ----- | ---------- |
| 1 minute | 5 uploads | `upload.rate_limit_minute` |
| 1 hour | 20 uploads | `upload.rate_limit_hour` |

Use same rolling-window pattern as OTP limits (Phase 01 sub-phase 03).

### Storage layout

```
public/report-photos/{photoId}/original.webp
public/report-photos/{photoId}/thumb.webp
private/report-photos/{photoId}/original.webp
private/report-photos/{photoId}/thumb.webp
```

Store keys on `ReportPhoto` entity.

---

## 5. Step-by-step implementation order

1. Add `IImageProcessor` — EXIF strip, WebP encode, thumbnail resize
2. Add `IObjectStorage` abstraction over S3 SDK (Railway Buckets endpoint from env)
3. Implement `ReportPhotoUploadService`
4. Add rate-limit tracking table or reuse generic rate-limit store from Phase 01
5. Add `UploadsController` with both actions
6. Configure `RequestSizeLimit` / `MultipartBodyLengthLimit` for 5 MB
7. Write integration tests with fake storage + test image fixtures
8. Manual test: upload JPEG with EXIF → verify stripped in stored file

---

## 6. Out of scope

- Linking photos to reports (sub-phase 05)
- Claim photos and chat attachments (Phases 05–06)
- Orphan cleanup job (Phase 07 — add `ExpiresAt` column now if helpful)
- Angular upload UI (sub-phase 07)

---

## 7. Validation gate

### Automated tests

- [ ] Valid JPEG upload → `201` with photo `id`
- [ ] File > 5 MB → `400 validation.file_too_large`
- [ ] Invalid content type → `400`
- [ ] Unauthenticated → `401`
- [ ] 6th upload in 1 minute → `429 upload.rate_limit_minute`
- [ ] Documents/IDs category → stored under private prefix
- [ ] Phones category → stored under public prefix
- [ ] EXIF GPS tag removed from processed image (assert in test)
- [ ] Private photo URL endpoint returns presigned URL for reporter
- [ ] Private photo URL for different user → `404`
- [ ] Admin can get private photo URL

### Manual smoke checklist

- [ ] Upload photo via Swagger; inspect bucket keys in storage browser
- [ ] Open thumbnail URL in browser — image renders

---

## 8. Exit criteria

- [ ] All automated tests pass
- [ ] Image pipeline produces WebP thumbnails
- [ ] Mark sub-phase 04 complete in [phase-02/README.md](./README.md)
