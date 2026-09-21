using Amanah.Api.Data;
using Amanah.Api.Data.Entities;
using Amanah.Api.Models.Errors;
using Amanah.Api.Services.Abuse;
using Amanah.Api.Services.Storage;
using Amanah.Contracts.Errors;
using Amanah.Contracts.Responses.Uploads;
using Microsoft.EntityFrameworkCore;

namespace Amanah.Api.Services.Uploads;

public sealed class ClaimPhotoPresignService(
    AppDbContext dbContext,
    IBucketStorage bucketStorage,
    FlaggedListingInvestigationService investigationService)
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
            if (!await investigationService.IsInvestigationOpenForReportAsync(
                    claim.ReportId,
                    cancellationToken))
            {
                return ResultError.Forbidden(
                    "Investigation access is only available while an abuse report is open.",
                    ErrorCodes.AbuseInvestigationUnavailable);
            }

            return await BuildPresignResponseAsync(claim.PhotoStorageKey, cancellationToken);
        }

        if (claim.Report.ReporterId != userId && claim.ClaimantId != userId)
        {
            return ResultError.NotFound("Photo not found.");
        }

        return await BuildPresignResponseAsync(claim.PhotoStorageKey, cancellationToken);
    }

    private async Task<Result<ClaimPhotoPresignResponse>> BuildPresignResponseAsync(
        string photoStorageKey,
        CancellationToken cancellationToken)
    {
        var thumbnailKey = ClaimPhotoStorageKeys.ThumbnailForOriginal(photoStorageKey);
        var storageKey = thumbnailKey is not null
            && await bucketStorage.ExistsAsync(thumbnailKey, cancellationToken)
            ? thumbnailKey
            : photoStorageKey;

        var url = bucketStorage.GetPreSignedUrl(storageKey, TimeSpan.FromMinutes(5));

        return new ClaimPhotoPresignResponse
        {
            Url = url.ToString(),
        };
    }
}
