using Amanah.Api.Data;
using Amanah.Api.Data.Entities;
using Amanah.Api.Models.Errors;
using Amanah.Api.Services.Notifications;
using Amanah.Api.Utilities.Claims;
using Amanah.Api.Utilities.Notifications;
using Amanah.Contracts.Errors;
using Amanah.Contracts.Requests.Claims;
using Amanah.Contracts.Responses.Browse;
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

    public const string AutoRejectReason = "Another claim approved";

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

        dbContext.Notifications.Add(CreateNotification(
            report.ReporterId,
            NotificationTypes.NewClaimSubmitted,
            new NotificationPayload(
                NotificationTypes.NewClaimSubmitted,
                now,
                DeepLink: $"/my/reports/{reportId}#claims-section",
                ReportId: reportId),
            now));

        await dbContext.SaveChangesAsync(cancellationToken);

        return new SubmitClaimResponse
        {
            Id = claim.Id,
            Status = MapClaimStatus(claim.Status),
        };
    }

    public async Task<Result> ApproveAsync(
        Guid claimId,
        Guid reporterId,
        CancellationToken cancellationToken = default)
    {
        var claim = await dbContext.Claims
            .Include(existingClaim => existingClaim.Report)
            .SingleOrDefaultAsync(existingClaim => existingClaim.Id == claimId, cancellationToken);

        if (claim is null || claim.Report.ReporterId != reporterId)
        {
            return ResultError.NotFound("Claim not found.");
        }

        if (claim.Status != ClaimStatus.Pending)
        {
            return ResultError.Conflict("Only pending claims can be approved.");
        }

        if (claim.Report.Status != ReportStatus.Published)
        {
            return ResultError.Conflict("Claims can only be approved on published reports.");
        }

        var now = timeProvider.GetUtcNow();

        claim.Status = ClaimStatus.Approved;
        claim.ReviewedAt = now;
        claim.ReviewerDecision = "approved";

        claim.Report.Status = ReportStatus.ClaimInProgress;
        claim.Report.UpdatedAt = now;

        // Drop any leftover Resolution from a prior cancelled claim (or a confirm/cancel
        // race) so the new approved claim always starts with a clean mutual-confirm slate.
        var staleResolution = await dbContext.Resolutions
            .SingleOrDefaultAsync(
                existingResolution => existingResolution.ReportId == claim.ReportId,
                cancellationToken);
        if (staleResolution is not null)
        {
            dbContext.Resolutions.Remove(staleResolution);
        }

        // Phase 05 activates messaging on this thread; until then it is a placeholder record only.
        var chatThread = new ChatThread
        {
            Id = Guid.NewGuid(),
            ClaimId = claim.Id,
            CreatedAt = now,
        };

        dbContext.ChatThreads.Add(chatThread);

        var otherPendingClaims = await dbContext.Claims
            .Where(existingClaim =>
                existingClaim.ReportId == claim.ReportId
                && existingClaim.Status == ClaimStatus.Pending
                && existingClaim.Id != claim.Id)
            .ToListAsync(cancellationToken);

        foreach (var otherClaim in otherPendingClaims)
        {
            otherClaim.Status = ClaimStatus.Rejected;
            otherClaim.ReviewedAt = now;
            otherClaim.ReviewerDecision = "rejected";
            otherClaim.DecisionReason = AutoRejectReason;
            otherClaim.CountsAsFailure = false;

            dbContext.Notifications.Add(CreateNotification(
                otherClaim.ClaimantId,
                NotificationTypes.ClaimRejected,
                new NotificationPayload(
                    NotificationTypes.ClaimRejected,
                    now,
                    DeepLink: BuildReportDeepLink(claim.Report),
                    ReportId: claim.Report.Id,
                    Note: AutoRejectReason),
                now));
        }

        dbContext.Notifications.Add(CreateNotification(
            claim.ClaimantId,
            NotificationTypes.ClaimApproved,
            new NotificationPayload(
                NotificationTypes.ClaimApproved,
                now,
                DeepLink: $"/my/chats/{chatThread.Id}",
                ReportId: claim.Report.Id),
            now));

        await dbContext.SaveChangesAsync(cancellationToken);
        return Result.Ok();
    }

    public async Task<Result> RejectAsync(
        Guid claimId,
        Guid reporterId,
        CancellationToken cancellationToken = default)
    {
        var claim = await dbContext.Claims
            .Include(existingClaim => existingClaim.Report)
            .SingleOrDefaultAsync(existingClaim => existingClaim.Id == claimId, cancellationToken);

        if (claim is null || claim.Report.ReporterId != reporterId)
        {
            return ResultError.NotFound("Claim not found.");
        }

        if (claim.Status != ClaimStatus.Pending)
        {
            return ResultError.Conflict("Only pending claims can be rejected.");
        }

        var now = timeProvider.GetUtcNow();

        claim.Status = ClaimStatus.Rejected;
        claim.ReviewedAt = now;
        claim.ReviewerDecision = "rejected";
        claim.CountsAsFailure = true;

        dbContext.Notifications.Add(CreateNotification(
            claim.ClaimantId,
            NotificationTypes.ClaimRejected,
            new NotificationPayload(
                NotificationTypes.ClaimRejected,
                now,
                DeepLink: BuildReportDeepLink(claim.Report),
                ReportId: claim.Report.Id),
            now));

        await dbContext.SaveChangesAsync(cancellationToken);
        return Result.Ok();
    }

    public async Task<Result> WithdrawAsync(
        Guid claimId,
        Guid claimantId,
        CancellationToken cancellationToken = default)
    {
        var claim = await dbContext.Claims
            .Include(existingClaim => existingClaim.Report)
            .SingleOrDefaultAsync(existingClaim => existingClaim.Id == claimId, cancellationToken);

        if (claim is null || claim.ClaimantId != claimantId)
        {
            return ResultError.NotFound("Claim not found.");
        }

        if (claim.Status != ClaimStatus.Pending)
        {
            return ResultError.Conflict("Only pending claims can be withdrawn.");
        }

        var now = timeProvider.GetUtcNow();

        claim.Status = ClaimStatus.Withdrawn;
        claim.ReviewedAt = now;
        claim.CountsAsFailure = false;

        dbContext.Notifications.Add(CreateNotification(
            claim.Report.ReporterId,
            NotificationTypes.ClaimWithdrawnByClaimant,
            new NotificationPayload(
                NotificationTypes.ClaimWithdrawnByClaimant,
                now,
                DeepLink: $"/my/reports/{claim.ReportId}",
                ReportId: claim.ReportId),
            now));

        await dbContext.SaveChangesAsync(cancellationToken);
        return Result.Ok();
    }

    public async Task<Result<PaginatedResponse<MyClaimSummaryResponse>>> GetMineAsync(
        Guid claimantId,
        MyClaimsQuery query,
        CancellationToken cancellationToken = default)
    {
        if (query.Page < 1)
        {
            return ResultError.BadRequest(
                "Please correct the errors in the form.",
                errors: new Dictionary<string, string[]>
                {
                    ["page"] = ["Page must be at least 1."],
                });
        }

        if (query.PageSize is < 1 or > 50)
        {
            return ResultError.BadRequest(
                "Please correct the errors in the form.",
                errors: new Dictionary<string, string[]>
                {
                    ["pageSize"] = ["Page size must be between 1 and 50."],
                });
        }

        var claimsQuery = dbContext.Claims
            .AsNoTracking()
            .Include(claim => claim.Report)
            .ThenInclude(report => report.Reporter)
            .Where(claim => claim.ClaimantId == claimantId);

        var totalCount = await claimsQuery.CountAsync(cancellationToken);

        var claims = await claimsQuery
            .OrderByDescending(claim => claim.SubmittedAt)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(cancellationToken);

        var totalPages = totalCount == 0
            ? 0
            : (int)Math.Ceiling(totalCount / (double)query.PageSize);

        return new PaginatedResponse<MyClaimSummaryResponse>
        {
            Items = claims.Select(ToMyClaimSummary).ToList(),
            Page = query.Page,
            PageSize = query.PageSize,
            TotalCount = totalCount,
            TotalPages = totalPages,
        };
    }

    public async Task<Result<ClaimDetailResponse>> GetByIdAsync(
        Guid claimId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var claim = await dbContext.Claims
            .AsNoTracking()
            .Include(existingClaim => existingClaim.Report)
            .ThenInclude(report => report.Reporter)
            .Include(existingClaim => existingClaim.Claimant)
            .Include(existingClaim => existingClaim.ChatThread)
            .SingleOrDefaultAsync(existingClaim => existingClaim.Id == claimId, cancellationToken);

        if (claim is null)
        {
            return ResultError.NotFound("Claim not found.");
        }

        var isClaimant = claim.ClaimantId == userId;
        var isReporter = claim.Report.ReporterId == userId;
        if (!isClaimant && !isReporter)
        {
            return ResultError.NotFound("Claim not found.");
        }

        return ToClaimDetail(claim);
    }

    public async Task<Result<IReadOnlyList<ReportClaimSummaryResponse>>> GetByReportAsync(
        Guid reportId,
        Guid reporterId,
        CancellationToken cancellationToken = default)
    {
        var report = await dbContext.Reports
            .AsNoTracking()
            .SingleOrDefaultAsync(existingReport => existingReport.Id == reportId, cancellationToken);

        if (report is null || report.ReporterId != reporterId)
        {
            return ResultError.NotFound("Report not found.");
        }

        var claims = await dbContext.Claims
            .AsNoTracking()
            .Include(claim => claim.Claimant)
            .Where(claim => claim.ReportId == reportId)
            .OrderByDescending(claim => claim.SubmittedAt)
            .ToListAsync(cancellationToken);

        return claims.Select(ToReportClaimSummary).ToList();
    }

    private static Notification CreateNotification(
        Guid userId,
        string type,
        NotificationPayload payload,
        DateTimeOffset createdAt) =>
        new()
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Type = type,
            PayloadJson = payload.ToJson(),
            IsRead = false,
            CreatedAt = createdAt,
        };

    private static string BuildReportDeepLink(Report report) => report.Type switch
    {
        ReportType.Lost => $"/lost/{report.Id}",
        ReportType.Found => $"/found/{report.Id}",
        _ => $"/reports/{report.Id}",
    };

    private static ClaimDetailResponse ToClaimDetail(Claim claim) =>
        new()
        {
            Id = claim.Id,
            Status = MapClaimStatus(claim.Status),
            SubmittedAnswer = claim.SubmittedAnswer,
            HasPhoto = !string.IsNullOrWhiteSpace(claim.PhotoStorageKey),
            SubmittedAt = claim.SubmittedAt,
            ReviewedAt = claim.ReviewedAt,
            ReviewerDecision = claim.ReviewerDecision,
            DecisionReason = claim.DecisionReason,
            AttemptNumber = claim.AttemptNumber,
            ChatThreadId = claim.ChatThread?.Id,
            ReportId = claim.ReportId,
            ReportType = MapReportType(claim.Report.Type),
            ReportStatus = MapReportStatus(claim.Report.Status),
            ReportTitle = claim.Report.Title,
            ClaimantDisplayName = claim.Claimant.DisplayName ?? string.Empty,
            ReporterDisplayName = claim.Report.Reporter.DisplayName ?? string.Empty,
        };

    private static ReportClaimSummaryResponse ToReportClaimSummary(Claim claim) =>
        new()
        {
            Id = claim.Id,
            Status = MapClaimStatus(claim.Status),
            SubmittedAnswer = claim.SubmittedAnswer,
            HasPhoto = !string.IsNullOrWhiteSpace(claim.PhotoStorageKey),
            SubmittedAt = claim.SubmittedAt,
            ReviewedAt = claim.ReviewedAt,
            DecisionReason = claim.DecisionReason,
            AttemptNumber = claim.AttemptNumber,
            ClaimantDisplayName = claim.Claimant.DisplayName ?? string.Empty,
        };

    private static MyClaimSummaryResponse ToMyClaimSummary(Claim claim) =>
        new()
        {
            Id = claim.Id,
            Status = MapClaimStatus(claim.Status),
            SubmittedAt = claim.SubmittedAt,
            ReviewedAt = claim.ReviewedAt,
            DecisionReason = claim.DecisionReason,
            ReportId = claim.ReportId,
            ReportType = MapReportType(claim.Report.Type),
            ReportTitle = claim.Report.Title,
            ReporterDisplayName = claim.Report.Reporter.DisplayName ?? string.Empty,
        };

    private static string MapReportType(ReportType type) => type switch
    {
        ReportType.Lost => "lost",
        ReportType.Found => "found",
        _ => type.ToString().ToLowerInvariant(),
    };

    private static string MapReportStatus(ReportStatus status) => status switch
    {
        ReportStatus.PendingReview => "pending_review",
        ReportStatus.Rejected => "rejected",
        ReportStatus.Published => "published",
        ReportStatus.ClaimInProgress => "claim_in_progress",
        ReportStatus.Resolved => "resolved",
        ReportStatus.Withdrawn => "withdrawn",
        ReportStatus.RemovedByAdmin => "removed_by_admin",
        _ => status.ToString().ToLowerInvariant(),
    };

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
