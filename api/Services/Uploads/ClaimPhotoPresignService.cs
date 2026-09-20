using Amanah.Api.Data;
using Amanah.Api.Data.Entities;
using Amanah.Api.Models.Errors;
using Amanah.Api.Services.Storage;
using Amanah.Contracts.Responses.Uploads;
using Microsoft.EntityFrameworkCore;

namespace Amanah.Api.Services.Uploads;

public sealed class ClaimPhotoPresignService(
    AppDbContext dbContext,
    IBucketStorage bucketStorage)
{
    public async Task<Result<ClaimPhotoPresignResponse>> GetClaimPhotoUrlAsync(
        Guid claimId,
        Guid userId,
        UserRole role,
        CancellationToken cancellationToken = default)
    {
        var claim = await dbContext.Claims
            .AsNoTracking()
            .Include(existingClaim => existingClaim.Report)
            .SingleOrDefaultAsync(existingClaim => existingClaim.Id == claimId, cancellationToken);

        if (claim is null || string.IsNullOrWhiteSpace(claim.PhotoStorageKey))
        {
            return ResultError.NotFound("Photo not found.");
        }

        if (role == UserRole.Admin)
        {
            // Phase 07: allow admin access during flagged-listing investigation only.
            return ResultError.Forbidden("Admin claim photo access is not available yet.");
        }

        if (claim.Report.ReporterId != userId && claim.ClaimantId != userId)
        {
            return ResultError.NotFound("Photo not found.");
        }

        var thumbnailKey = ClaimPhotoStorageKeys.ThumbnailForOriginal(claim.PhotoStorageKey);
        var storageKey = thumbnailKey is not null
            && await bucketStorage.ExistsAsync(thumbnailKey, cancellationToken)
            ? thumbnailKey
            : claim.PhotoStorageKey;

        var url = bucketStorage.GetPreSignedUrl(storageKey, TimeSpan.FromMinutes(5));

        return new ClaimPhotoPresignResponse
        {
            Url = url.ToString(),
        };
    }
}
