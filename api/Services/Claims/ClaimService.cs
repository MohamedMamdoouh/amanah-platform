using Amanah.Api.Data;
using Amanah.Api.Data.Entities;
using Amanah.Api.Models.Errors;
using Amanah.Api.Options;
using Amanah.Api.Services.Lifecycle;
using Amanah.Api.Services.Notifications;
using Amanah.Api.Utilities.Claims;
using Amanah.Api.Utilities.Common;
using Amanah.Api.Utilities.Notifications;
using Amanah.Api.Utilities.Reports;
using Amanah.Api.Utilities.Resolution;
using Amanah.Contracts.Errors;
using Amanah.Contracts.Requests.Claims;
using Amanah.Contracts.Responses.Browse;
using Amanah.Contracts.Responses.Claims;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Amanah.Api.Services.Claims;

public sealed record ClaimQuotaCheckResult(bool IsExceeded, int? RetryAfterSeconds = null);

public sealed class ClaimService(
    AppDbContext dbContext,
    ClaimPhotoAttachService claimPhotoAttachService,
    ReportLifecycleService reportLifecycleService,
    TimeProvider timeProvider,
    IOptions<LifecycleOptions> lifecycleOptions)
{
    public const int MaxCountedFailures = 3;

    public const int DailyQuotaLimit = 5;

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

        var normalizedAnswer = TextNormalizer.Normalize(request.SubmittedAnswer);

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

        var quotaResult = await CheckDailySubmissionAsync(claimantId, cancellationToken);
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

        dbContext.Notifications.Add(new Notification
        {
            Id = Guid.NewGuid(),
            UserId = report.ReporterId,
            Type = NotificationTypes.NewClaimSubmitted,
            PayloadJson = new NotificationPayload(
                NotificationTypes.NewClaimSubmitted,
                now,
                DeepLink: $"/my/reports/{reportId}#claims-section",
                ReportId: reportId).ToJson(),
            IsRead = false,
            CreatedAt = now,
        });

        await dbContext.SaveChangesAsync(cancellationToken);

        return new SubmitClaimResponse
        {
            Id = claim.Id,
            Status = ClaimApiStrings.ToStatus(claim.Status),
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
        reportLifecycleService.PausePublishedTimer(claim.Report, now);
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

        // ChatThread is created on claim approval; messaging is served via Phase 05 hub and REST endpoints.
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

            dbContext.Notifications.Add(new Notification
            {
                Id = Guid.NewGuid(),
                UserId = otherClaim.ClaimantId,
                Type = NotificationTypes.ClaimRejected,
                PayloadJson = new NotificationPayload(
                    NotificationTypes.ClaimRejected,
                    now,
                    DeepLink: claim.Report.Type switch
                    {
                        ReportType.Lost => $"/lost/{claim.Report.Id}",
                        ReportType.Found => $"/found/{claim.Report.Id}",
                        _ => $"/reports/{claim.Report.Id}",
                    },
                    ReportId: claim.Report.Id,
                    Note: AutoRejectReason).ToJson(),
                IsRead = false,
                CreatedAt = now,
            });
        }

        dbContext.Notifications.Add(new Notification
        {
            Id = Guid.NewGuid(),
            UserId = claim.ClaimantId,
            Type = NotificationTypes.ClaimApproved,
            PayloadJson = new NotificationPayload(
                NotificationTypes.ClaimApproved,
                now,
                DeepLink: $"/my/chats/{chatThread.Id}",
                ReportId: claim.Report.Id).ToJson(),
            IsRead = false,
            CreatedAt = now,
        });

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

        dbContext.Notifications.Add(new Notification
        {
            Id = Guid.NewGuid(),
            UserId = claim.ClaimantId,
            Type = NotificationTypes.ClaimRejected,
            PayloadJson = new NotificationPayload(
                NotificationTypes.ClaimRejected,
                now,
                DeepLink: claim.Report.Type switch
                {
                    ReportType.Lost => $"/lost/{claim.Report.Id}",
                    ReportType.Found => $"/found/{claim.Report.Id}",
                    _ => $"/reports/{claim.Report.Id}",
                },
                ReportId: claim.Report.Id).ToJson(),
            IsRead = false,
            CreatedAt = now,
        });

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

        dbContext.Notifications.Add(new Notification
        {
            Id = Guid.NewGuid(),
            UserId = claim.Report.ReporterId,
            Type = NotificationTypes.ClaimWithdrawnByClaimant,
            PayloadJson = new NotificationPayload(
                NotificationTypes.ClaimWithdrawnByClaimant,
                now,
                DeepLink: $"/my/reports/{claim.ReportId}",
                ReportId: claim.ReportId).ToJson(),
            IsRead = false,
            CreatedAt = now,
        });

        await dbContext.SaveChangesAsync(cancellationToken);
        return Result.Ok();
    }

    public async Task<int> ProcessPendingClaimTimeoutsAsync(CancellationToken cancellationToken = default)
    {
        var now = timeProvider.GetUtcNow();
        var timeoutThreshold = now.AddMinutes(-lifecycleOptions.Value.ClaimTimeoutMinutes);
        // Snapshot ids only — a concurrent Approve can move a claim to Approved /
        // ClaimInProgress after this query. Blindly mutating tracked entities and
        // SaveChanges would overwrite that approval and leave the report stuck
        // (ClaimInProgress + Withdrawn claim: cancel and withdraw both blocked).
        var timedOutClaimIds = await dbContext.Claims
            .AsNoTracking()
            .Where(claim =>
                claim.Status == ClaimStatus.Pending
                && claim.SubmittedAt <= timeoutThreshold
                && claim.Report.Status == ReportStatus.Published)
            .Select(claim => claim.Id)
            .ToListAsync(cancellationToken);

        if (timedOutClaimIds.Count == 0)
        {
            return 0;
        }

        var withdrawnCount = 0;
        foreach (var claimId in timedOutClaimIds)
        {
            var updatedRows = await dbContext.Claims
                .Where(claim =>
                    claim.Id == claimId
                    && claim.Status == ClaimStatus.Pending
                    && claim.Report.Status == ReportStatus.Published)
                .ExecuteUpdateAsync(
                    setters => setters
                        .SetProperty(claim => claim.Status, ClaimStatus.Withdrawn)
                        .SetProperty(claim => claim.ReviewedAt, now)
                        .SetProperty(claim => claim.CountsAsFailure, false),
                    cancellationToken);

            if (updatedRows == 0)
            {
                continue;
            }

            var claim = await dbContext.Claims
                .AsNoTracking()
                .Include(existingClaim => existingClaim.Report)
                .SingleAsync(existingClaim => existingClaim.Id == claimId, cancellationToken);

            dbContext.Notifications.Add(new Notification
            {
                Id = Guid.NewGuid(),
                UserId = claim.Report.ReporterId,
                Type = NotificationTypes.ClaimAutoWithdrawn,
                PayloadJson = new NotificationPayload(
                    NotificationTypes.ClaimAutoWithdrawn,
                    now,
                    DeepLink: $"/my/reports/{claim.ReportId}#claims-section",
                    ReportId: claim.ReportId).ToJson(),
                IsRead = false,
                CreatedAt = now,
            });

            dbContext.Notifications.Add(new Notification
            {
                Id = Guid.NewGuid(),
                UserId = claim.ClaimantId,
                Type = NotificationTypes.ClaimAutoWithdrawn,
                PayloadJson = new NotificationPayload(
                    NotificationTypes.ClaimAutoWithdrawn,
                    now,
                    DeepLink: claim.Report.Type switch
                    {
                        ReportType.Lost => $"/lost/{claim.Report.Id}",
                        ReportType.Found => $"/found/{claim.Report.Id}",
                        _ => $"/reports/{claim.Report.Id}",
                    },
                    ReportId: claim.ReportId).ToJson(),
                IsRead = false,
                CreatedAt = now,
            });

            withdrawnCount++;
        }

        if (withdrawnCount > 0)
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return withdrawnCount;
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

        return Pagination.Create(
            claims.Select(ToMyClaimSummary).ToList(),
            query.Page,
            query.PageSize,
            totalCount);
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
            .Include(existingClaim => existingClaim.Report)
            .ThenInclude(report => report.Resolution)
            .Include(existingClaim => existingClaim.Claimant)
            .Include(existingClaim => existingClaim.ChatThread)
            .SingleOrDefaultAsync(existingClaim => existingClaim.Id == claimId, cancellationToken);

        if (claim is null)
        {
            return ResultError.NotFound("Claim not found.");
        }

        if (claim.Report.ReporterId != userId && claim.ClaimantId != userId)
        {
            return ResultError.NotFound("Claim not found.");
        }

        return ToClaimDetail(claim, userId);
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

    private static ClaimDetailResponse ToClaimDetail(Claim claim, Guid userId)
    {
        var isReporter = claim.Report.ReporterId == userId;

        return new()
        {
            Id = claim.Id,
            Status = ClaimApiStrings.ToStatus(claim.Status),
            SubmittedAnswer = claim.SubmittedAnswer,
            HasPhoto = !string.IsNullOrWhiteSpace(claim.PhotoStorageKey),
            SubmittedAt = claim.SubmittedAt,
            ReviewedAt = claim.ReviewedAt,
            ReviewerDecision = claim.ReviewerDecision,
            DecisionReason = claim.DecisionReason,
            AttemptNumber = claim.AttemptNumber,
            ChatThreadId = claim.ChatThread?.Id,
            ReportId = claim.ReportId,
            ReportType = ReportApiStrings.ToType(claim.Report.Type),
            ReportStatus = ReportApiStrings.ToStatus(claim.Report.Status),
            ReportTitle = claim.Report.Title,
            ClaimantDisplayName = claim.Claimant.DisplayName ?? string.Empty,
            ReporterDisplayName = claim.Report.Reporter.DisplayName ?? string.Empty,
            Resolution = ResolutionStateMapper.FromClaim(claim, isReporter),
        };
    }

    private static ReportClaimSummaryResponse ToReportClaimSummary(Claim claim) =>
        new()
        {
            Id = claim.Id,
            Status = ClaimApiStrings.ToStatus(claim.Status),
            SubmittedAnswer = claim.SubmittedAnswer,
            HasPhoto = !string.IsNullOrWhiteSpace(claim.PhotoStorageKey),
            SubmittedAt = claim.SubmittedAt,
            ReviewedAt = claim.ReviewedAt,
            DecisionReason = claim.DecisionReason,
            AttemptNumber = claim.AttemptNumber,
            ClaimantDisplayName = claim.Claimant.DisplayName ?? string.Empty,
        };

    private async Task<ClaimQuotaCheckResult> CheckDailySubmissionAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var dailyCount = await CountClaimsSubmittedTodayAsync(userId, cancellationToken);
        if (dailyCount < DailyQuotaLimit)
        {
            return new ClaimQuotaCheckResult(false);
        }

        return new ClaimQuotaCheckResult(true, SecondsUntilNextCairoMidnight());
    }

    private async Task<int> CountClaimsSubmittedTodayAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var dayStart = CairoTime.CairoDayStartUtc(now);
        var nextDayStart = dayStart.AddDays(1);

        return await dbContext.Claims
            .AsNoTracking()
            .CountAsync(
                claim => claim.ClaimantId == userId
                    && claim.SubmittedAt >= dayStart
                    && claim.SubmittedAt < nextDayStart,
                cancellationToken);
    }

    private int SecondsUntilNextCairoMidnight()
    {
        var now = timeProvider.GetUtcNow();
        var dayStart = CairoTime.CairoDayStartUtc(now);
        var nextDayStart = dayStart.AddDays(1);
        var seconds = (int)Math.Ceiling((nextDayStart - now).TotalSeconds);

        return Math.Max(seconds, 1);
    }

    private static MyClaimSummaryResponse ToMyClaimSummary(Claim claim) =>
        new()
        {
            Id = claim.Id,
            Status = ClaimApiStrings.ToStatus(claim.Status),
            SubmittedAt = claim.SubmittedAt,
            ReviewedAt = claim.ReviewedAt,
            DecisionReason = claim.DecisionReason,
            ReportId = claim.ReportId,
            ReportType = ReportApiStrings.ToType(claim.Report.Type),
            ReportTitle = claim.Report.Title,
            ReporterDisplayName = claim.Report.Reporter.DisplayName ?? string.Empty,
        };
}
