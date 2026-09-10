using Amanah.Api.Data;
using Amanah.Api.Data.Entities;
using Amanah.Api.Models.Errors;
using Amanah.Api.Utilities.Claims;
using Amanah.Contracts.Errors;
using Amanah.Contracts.Requests.Claims;
using Amanah.Contracts.Responses.Claims;
using Microsoft.EntityFrameworkCore;

namespace Amanah.Api.Services.Claims;

public sealed class ClaimService(
    AppDbContext dbContext,
    IClaimQuotaService quotaService,
    ClaimPhotoAttachService claimPhotoAttachService,
    TimeProvider timeProvider) : IClaimService
{
    public const int MaxCountedFailures = 3;

    public async Task<Result<SubmitClaimResponse>> SubmitAsync(
        Guid reportId,
        Guid claimantId,
        SubmitClaimRequest request,
        IFormFile? photo,
        CancellationToken cancellationToken = default)
    {
        var validationErrors = ClaimContentValidator.Validate(request.SubmittedAnswer);
        if (validationErrors is not null)
        {
            return ResultError.BadRequest(
                "Please correct the errors in the form.",
                errors: validationErrors);
        }

        var normalizedAnswer = ClaimContentValidator.NormalizeAnswer(request.SubmittedAnswer);

        var report = await dbContext.Reports
            .AsNoTracking()
            .SingleOrDefaultAsync(existingReport => existingReport.Id == reportId, cancellationToken);

        if (report is null)
        {
            return ResultError.NotFound("Report not found.");
        }

        if (report.Status != ReportStatus.Published)
        {
            return ResultError.Conflict(
                "Claims can only be submitted on published reports.",
                ErrorCodes.ClaimInvalidStatus);
        }

        if (report.ReporterId == claimantId)
        {
            return ResultError.Conflict(
                "You cannot claim your own report.",
                ErrorCodes.ClaimOwnReport);
        }

        var existingClaims = await dbContext.Claims
            .AsNoTracking()
            .Where(claim => claim.ReportId == reportId && claim.ClaimantId == claimantId)
            .ToListAsync(cancellationToken);

        if (existingClaims.Any(claim => claim.Status == ClaimStatus.Pending))
        {
            return ResultError.Conflict(
                "You already have a pending claim on this report.",
                ErrorCodes.ClaimPendingExists);
        }

        var failureCount = existingClaims.Count(claim => claim.CountsAsFailure);
        if (failureCount >= MaxCountedFailures)
        {
            return ResultError.Conflict(
                "You have used all 3 claim attempts on this report.",
                ErrorCodes.ClaimAttemptLimit);
        }

        var quotaResult = await quotaService.CheckDailySubmissionAsync(claimantId, cancellationToken);
        if (quotaResult.IsExceeded)
        {
            return ResultError.TooManyRequests(
                "You have reached the daily limit of 5 claim submissions. Try again after midnight (Cairo time).",
                quotaResult.RetryAfterSeconds ?? 1,
                ErrorCodes.ClaimDailyQuota);
        }

        var attemptNumber = existingClaims.Count == 0
            ? 1
            : existingClaims.Max(claim => claim.AttemptNumber) + 1;

        var now = timeProvider.GetUtcNow();
        var claim = new Claim
        {
            Id = Guid.NewGuid(),
            ReportId = reportId,
            ClaimantId = claimantId,
            Status = ClaimStatus.Pending,
            SubmittedAnswer = normalizedAnswer,
            SubmittedAt = now,
            AttemptNumber = attemptNumber,
            CountsAsFailure = false,
        };

        if (photo is not null)
        {
            var photoResult = await claimPhotoAttachService.AttachAsync(claim.Id, photo, cancellationToken);
            if (!photoResult.IsSuccess)
            {
                return photoResult.Error!;
            }

            claim.PhotoStorageKey = photoResult.Value;
        }

        dbContext.Claims.Add(claim);
        await dbContext.SaveChangesAsync(cancellationToken);

        return new SubmitClaimResponse
        {
            Id = claim.Id,
            Status = MapClaimStatus(claim.Status),
        };
    }

    private static string MapClaimStatus(ClaimStatus status) => status switch
    {
        ClaimStatus.Pending => "pending",
        ClaimStatus.Approved => "approved",
        ClaimStatus.Rejected => "rejected",
        ClaimStatus.Withdrawn => "withdrawn",
        ClaimStatus.Cancelled => "cancelled",
        _ => status.ToString().ToLowerInvariant(),
    };
}
