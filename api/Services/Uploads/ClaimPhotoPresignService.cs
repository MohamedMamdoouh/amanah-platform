using Amanah.Api.Data;
using Amanah.Api.Data.Entities;
using Amanah.Api.Data.Extensions;
using Amanah.Api.Models.Errors;
using Amanah.Api.Services.Storage;
using Amanah.Api.Utilities.Claims;
using Amanah.Api.Utilities.Uploads;
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
            .WithReportInclude()
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

        if (!ClaimAccessAuthorization.IsReporterOrClaimant(claim, userId))
        {
            return ResultError.NotFound("Photo not found.");
        }

        var thumbnailKey = ClaimPhotoStorageKeys.ThumbnailForOriginal(claim.PhotoStorageKey);
        var storageKey = await StorageKeyResolver.ResolvePreferExistingThumbnailAsync(
            bucketStorage,
            claim.PhotoStorageKey,
            thumbnailKey,
            cancellationToken);

        var url = bucketStorage.GetPreSignedUrl(storageKey, PresignConstants.Lifetime);

        return new ClaimPhotoPresignResponse
        {
            Url = url.ToString(),
        };
    }
}
