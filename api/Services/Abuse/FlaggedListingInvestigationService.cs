using Amanah.Api.Data;
using Amanah.Api.Data.Entities;
using Amanah.Api.Models.Errors;
using Amanah.Api.Services.Chats;
using Amanah.Api.Services.Storage;
using Amanah.Api.Services.Uploads;
using Amanah.Contracts.Errors;
using Amanah.Contracts.Responses.Admin;
using Microsoft.EntityFrameworkCore;

namespace Amanah.Api.Services.Abuse;

public sealed class FlaggedListingInvestigationService(
    AppDbContext dbContext,
    ChatService chatService,
    IBucketStorage bucketStorage)
{
    public Task<bool> IsInvestigationOpenForReportAsync(
        Guid reportId,
        CancellationToken cancellationToken = default) =>
        dbContext.AbuseReports
            .AsNoTracking()
            .AnyAsync(
                abuseReport => abuseReport.ReportId == reportId
                    && abuseReport.Status == AbuseReportStatus.Open,
                cancellationToken);

    public async Task<Result<InvestigationChatResponse>> GetChatAsync(
        Guid reportId,
        CancellationToken cancellationToken = default)
    {
        if (await GetInvestigationAccessErrorAsync(reportId, cancellationToken) is { } accessError)
        {
            return accessError;
        }

        var threads = await chatService.GetInvestigationThreadsForReportAsync(reportId, cancellationToken);
        return new InvestigationChatResponse { Threads = threads };
    }

    public async Task<Result<InvestigationClaimsResponse>> GetClaimsAsync(
        Guid reportId,
        CancellationToken cancellationToken = default)
    {
        if (await GetInvestigationAccessErrorAsync(reportId, cancellationToken) is { } accessError)
        {
            return accessError;
        }

        var claims = await dbContext.Claims
            .AsNoTracking()
            .Include(claim => claim.Claimant)
            .Include(claim => claim.ChatThread)
            .Where(claim => claim.ReportId == reportId)
            .OrderByDescending(claim => claim.SubmittedAt)
            .ToListAsync(cancellationToken);

        var items = new List<InvestigationClaimResponse>(claims.Count);
        foreach (var claim in claims)
        {
            string? photoUrl = null;
            if (!string.IsNullOrWhiteSpace(claim.PhotoStorageKey))
            {
                photoUrl = await BuildClaimPhotoUrlAsync(claim.PhotoStorageKey, cancellationToken);
            }

            items.Add(new InvestigationClaimResponse
            {
                Id = claim.Id,
                Status = ToClaimStatus(claim.Status),
                SubmittedAnswer = claim.SubmittedAnswer,
                HasPhoto = !string.IsNullOrWhiteSpace(claim.PhotoStorageKey),
                PhotoUrl = photoUrl,
                SubmittedAt = claim.SubmittedAt,
                ReviewedAt = claim.ReviewedAt,
                DecisionReason = claim.DecisionReason,
                AttemptNumber = claim.AttemptNumber,
                ClaimantDisplayName = claim.Claimant.DisplayName ?? string.Empty,
                ChatThreadId = claim.ChatThread?.Id,
            });
        }

        return new InvestigationClaimsResponse { Items = items };
    }

    public async Task<Result<InvestigationPhotosResponse>> GetPhotosAsync(
        Guid reportId,
        CancellationToken cancellationToken = default)
    {
        if (await GetInvestigationAccessErrorAsync(reportId, cancellationToken) is { } accessError)
        {
            return accessError;
        }

        var report = await dbContext.Reports
            .AsNoTracking()
            .Include(existingReport => existingReport.Category)
            .Include(existingReport => existingReport.Photos)
            .SingleAsync(existingReport => existingReport.Id == reportId, cancellationToken);

        if (!report.Category.PhotosPrivate)
        {
            return new InvestigationPhotosResponse();
        }

        var photos = report.Photos
            .OrderBy(photo => photo.SortOrder)
            .Select(photo =>
            {
                var storageKey = photo.ThumbnailStorageKey ?? photo.StorageKey;
                var url = bucketStorage.GetPreSignedUrl(storageKey, TimeSpan.FromMinutes(5));
                return new InvestigationReportPhotoResponse
                {
                    Id = photo.Id,
                    Url = url.ToString(),
                    SortOrder = photo.SortOrder,
                };
            })
            .ToList();

        return new InvestigationPhotosResponse { Photos = photos };
    }

    private async Task<ResultError?> GetInvestigationAccessErrorAsync(
        Guid reportId,
        CancellationToken cancellationToken)
    {
        var reportExists = await dbContext.Reports
            .AsNoTracking()
            .AnyAsync(report => report.Id == reportId, cancellationToken);

        if (!reportExists)
        {
            return ResultError.NotFound("Report not found.");
        }

        if (!await IsInvestigationOpenForReportAsync(reportId, cancellationToken))
        {
            return ResultError.Forbidden(
                "Investigation access is only available while an abuse report is open.",
                ErrorCodes.AbuseInvestigationUnavailable);
        }

        return null;
    }

    private async Task<string> BuildClaimPhotoUrlAsync(
        string photoStorageKey,
        CancellationToken cancellationToken)
    {
        var thumbnailKey = ClaimPhotoStorageKeys.ThumbnailForOriginal(photoStorageKey);
        var storageKey = thumbnailKey is not null
            && await bucketStorage.ExistsAsync(thumbnailKey, cancellationToken)
            ? thumbnailKey
            : photoStorageKey;

        return bucketStorage.GetPreSignedUrl(storageKey, TimeSpan.FromMinutes(5)).ToString();
    }

    private static string ToClaimStatus(ClaimStatus status) => status switch
    {
        ClaimStatus.Pending => "pending",
        ClaimStatus.Approved => "approved",
        ClaimStatus.Rejected => "rejected",
        ClaimStatus.Withdrawn => "withdrawn",
        ClaimStatus.Cancelled => "cancelled",
        _ => status.ToString().ToLowerInvariant(),
    };
}
