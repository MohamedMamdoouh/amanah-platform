using Amanah.Api.Data;
using Amanah.Api.Data.Entities;
using Amanah.Api.Models.Errors;
using Amanah.Api.Services.Enforcement;
using Amanah.Api.Services.Notifications;
using Amanah.Api.Utilities.Abuse;
using Amanah.Api.Utilities.Notifications;
using Amanah.Contracts.Errors;
using Amanah.Contracts.Requests.Admin;
using Amanah.Contracts.Responses.Admin;
using Microsoft.EntityFrameworkCore;

namespace Amanah.Api.Services.Abuse;

public sealed class AbuseAdminService(
    AppDbContext dbContext,
    AdminTakedownService adminTakedownService,
    UserEnforcementService userEnforcementService,
    TimeProvider timeProvider)
{
    public async Task<Result<AbuseQueueResponse>> GetQueueAsync(
        CancellationToken cancellationToken = default)
    {
        var openReports = await dbContext.AbuseReports
            .AsNoTracking()
            .Include(abuseReport => abuseReport.AbuseReporter)
            .Include(abuseReport => abuseReport.Report)
            .ThenInclude(report => report.Category)
            .Where(abuseReport => abuseReport.Status == AbuseReportStatus.Open)
            .OrderBy(abuseReport => abuseReport.CreatedAt)
            .ToListAsync(cancellationToken);

        return new AbuseQueueResponse
        {
            Items = openReports.Select(ToQueueItem).ToList(),
            OpenCount = openReports.Count,
        };
    }

    public async Task<Result<AbuseReportDetailResponse>> GetDetailAsync(
        Guid abuseReportId,
        CancellationToken cancellationToken = default)
    {
        var abuseReport = await LoadAbuseReportAsync(abuseReportId, asNoTracking: true, cancellationToken);
        if (abuseReport is null)
        {
            return ResultError.NotFound("Abuse report not found.");
        }

        return ToDetail(abuseReport);
    }

    public async Task<Result<ResolveAbuseReportResponse>> ResolveAsync(
        Guid abuseReportId,
        Guid adminId,
        ResolveAbuseReportRequest request,
        CancellationToken cancellationToken = default)
    {
        var outcome = request.Outcome.Trim().ToLowerInvariant();
        if (!AbuseResolutionOutcomes.All.Contains(outcome))
        {
            return ResultError.BadRequest(
                "Resolution outcome is invalid.",
                ErrorCodes.AbuseInvalidOutcome);
        }

        var abuseReport = await LoadAbuseReportAsync(abuseReportId, asNoTracking: false, cancellationToken);
        if (abuseReport is null)
        {
            return ResultError.NotFound("Abuse report not found.");
        }

        if (abuseReport.Status != AbuseReportStatus.Open)
        {
            return ResultError.Conflict(
                "This abuse report is already resolved.",
                ErrorCodes.AbuseAlreadyResolved);
        }

        var adminNote = string.IsNullOrWhiteSpace(request.AdminNote)
            ? null
            : request.AdminNote.Trim();

        switch (outcome)
        {
            case AbuseResolutionOutcomes.Takedown:
            {
                var takedownResult = await adminTakedownService.TakeDownAsync(
                    abuseReport.ReportId,
                    adminId,
                    new AdminReportTakedownRequest { Note = adminNote },
                    cancellationToken);
                if (!takedownResult.IsSuccess)
                {
                    return takedownResult.Error!;
                }

                break;
            }

            case AbuseResolutionOutcomes.Ban:
            {
                var banReason = adminNote;
                if (string.IsNullOrEmpty(banReason))
                {
                    return ResultError.BadRequest("A ban reason is required in adminNote.");
                }

                var banTargetUserId = request.BanTargetUserId ?? abuseReport.Report.ReporterId;
                var userExists = await dbContext.Users
                    .AsNoTracking()
                    .AnyAsync(user => user.Id == banTargetUserId, cancellationToken);
                if (!userExists)
                {
                    return ResultError.NotFound("Ban target user not found.");
                }

                var banResult = await userEnforcementService.BanAsync(
                    banTargetUserId,
                    adminId,
                    new BanUserRequest { Reason = banReason },
                    cancellationToken);
                if (!banResult.IsSuccess)
                {
                    return banResult.Error!;
                }

                break;
            }
        }

        var now = timeProvider.GetUtcNow();
        abuseReport.Status = AbuseReportStatus.Resolved;
        abuseReport.ResolutionOutcome = outcome;
        abuseReport.ResolvedByUserId = adminId;
        abuseReport.ResolvedAt = now;

        EnqueueFlaggerNotification(abuseReport, outcome, now);

        await dbContext.SaveChangesAsync(cancellationToken);

        return new ResolveAbuseReportResponse
        {
            Id = abuseReport.Id,
            Status = "resolved",
            ResolutionOutcome = outcome,
            ResolvedAt = now,
        };
    }

    private async Task<AbuseReport?> LoadAbuseReportAsync(
        Guid abuseReportId,
        bool asNoTracking,
        CancellationToken cancellationToken)
    {
        IQueryable<AbuseReport> query = dbContext.AbuseReports
            .Include(abuseReport => abuseReport.AbuseReporter)
            .Include(abuseReport => abuseReport.Report)
            .ThenInclude(report => report.Category)
            .Include(abuseReport => abuseReport.Report)
            .ThenInclude(report => report.Governorate)
            .Include(abuseReport => abuseReport.Report)
            .ThenInclude(report => report.Reporter);

        if (asNoTracking)
        {
            query = query.AsNoTracking();
        }

        return await query.SingleOrDefaultAsync(
            abuseReport => abuseReport.Id == abuseReportId,
            cancellationToken);
    }

    private void EnqueueFlaggerNotification(
        AbuseReport abuseReport,
        string outcome,
        DateTimeOffset now)
    {
        var report = abuseReport.Report;
        var deepLink = report.Type switch
        {
            ReportType.Lost => $"/lost/{report.Id}",
            ReportType.Found => $"/found/{report.Id}",
            _ => $"/reports/{report.Id}",
        };

        dbContext.Notifications.Add(new Notification
        {
            Id = Guid.NewGuid(),
            UserId = abuseReport.AbuseReporterId,
            Type = NotificationTypes.AbuseReportResolvedForFlagger,
            PayloadJson = new NotificationPayload(
                NotificationTypes.AbuseReportResolvedForFlagger,
                now,
                DeepLink: deepLink,
                ReportId: report.Id,
                ReasonCode: outcome).ToJson(),
            IsRead = false,
            CreatedAt = now,
        });
    }

    private static AbuseQueueItemResponse ToQueueItem(AbuseReport abuseReport) =>
        new()
        {
            Id = abuseReport.Id,
            ReportId = abuseReport.ReportId,
            ReportType = ToReportType(abuseReport.Report.Type),
            ReportTitle = abuseReport.Report.Title,
            ReportStatus = ToReportStatus(abuseReport.Report.Status),
            Reason = abuseReport.Reason,
            AbuseReporterUserId = abuseReport.AbuseReporterId,
            AbuseReporterDisplayName = abuseReport.AbuseReporter.DisplayName ?? string.Empty,
            Status = ToAbuseStatus(abuseReport.Status),
            CreatedAt = abuseReport.CreatedAt,
        };

    private static AbuseReportDetailResponse ToDetail(AbuseReport abuseReport) =>
        new()
        {
            Id = abuseReport.Id,
            Reason = abuseReport.Reason,
            Note = abuseReport.Note,
            Status = ToAbuseStatus(abuseReport.Status),
            ResolutionOutcome = abuseReport.ResolutionOutcome,
            CreatedAt = abuseReport.CreatedAt,
            ResolvedAt = abuseReport.ResolvedAt,
            AbuseReporterUserId = abuseReport.AbuseReporterId,
            AbuseReporterDisplayName = abuseReport.AbuseReporter.DisplayName ?? string.Empty,
            Listing = ToListingSummary(abuseReport.Report),
        };

    private static FlaggedListingSummaryResponse ToListingSummary(Report report) =>
        new()
        {
            Id = report.Id,
            Type = ToReportType(report.Type),
            Status = ToReportStatus(report.Status),
            Title = report.Title,
            CategoryCode = report.Category.Code,
            GovernorateCode = report.Governorate.Code,
            ListingOwnerUserId = report.ReporterId,
            ListingOwnerDisplayName = report.Reporter.DisplayName ?? string.Empty,
            CreatedAt = report.CreatedAt,
            PublishedAt = report.PublishedAt,
        };

    private static string ToAbuseStatus(AbuseReportStatus status) => status switch
    {
        AbuseReportStatus.Open => "open",
        AbuseReportStatus.Resolved => "resolved",
        _ => status.ToString().ToLowerInvariant(),
    };

    private static string ToReportType(ReportType type) => type switch
    {
        ReportType.Lost => "lost",
        ReportType.Found => "found",
        _ => type.ToString().ToLowerInvariant(),
    };

    private static string ToReportStatus(ReportStatus status) => status switch
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
}
