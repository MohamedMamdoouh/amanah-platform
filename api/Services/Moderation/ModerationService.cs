using Amanah.Api.Data;
using Amanah.Api.Data.Entities;
using Amanah.Api.Models.Errors;
using Amanah.Api.Services.Lifecycle;
using Amanah.Api.Services.Notifications;
using Amanah.Api.Services.Reports;
using Amanah.Api.Utilities.Common;
using Amanah.Api.Utilities.Notifications;
using Amanah.Api.Utilities.Reports;
using Amanah.Contracts.Requests.Admin;
using Amanah.Contracts.Responses.Admin;
using Amanah.Contracts.Responses.Reports;
using Microsoft.EntityFrameworkCore;

namespace Amanah.Api.Services.Moderation;

public sealed class ModerationService(
    AppDbContext dbContext,
    ReportService reportService,
    ReportLifecycleService reportLifecycleService,
    TimeProvider timeProvider)
{
    public async Task<Result<ModerationQueueResponse>> GetQueueAsync(
        CancellationToken cancellationToken = default)
    {
        var reports = await dbContext.Reports
            .AsNoTracking()
            .Include(report => report.Category)
            .Where(report => report.Status == ReportStatus.PendingReview)
            .OrderBy(report => report.CreatedAt)
            .ToListAsync(cancellationToken);

        return new ModerationQueueResponse
        {
            Items = reports.Select(ToQueueItem).ToList(),
            PendingCount = reports.Count,
        };
    }

    public async Task<Result<ModerationSearchResponse>> SearchAsync(
        string? query,
        CancellationToken cancellationToken = default)
    {
        var terms = ArabicNormalizer.BuildSearchTerms(query ?? string.Empty);
        if (terms.Length == 0)
        {
            return new ModerationSearchResponse();
        }

        IQueryable<Report> reportsQuery = dbContext.Reports
            .AsNoTracking()
            .Include(report => report.Category)
            .Where(report =>
                report.Status == ReportStatus.PendingReview
                || report.Status == ReportStatus.Rejected);

        reportsQuery = SearchTextBuilder.FilterBySearchTerms(reportsQuery, terms);

        var reports = await reportsQuery
            .OrderByDescending(report => report.CreatedAt)
            .ToListAsync(cancellationToken);

        return new ModerationSearchResponse
        {
            Items = reports.Select(ToQueueItem).ToList(),
        };
    }

    public Task<Result<ReportDetailResponse>> GetReportAsync(
        Guid reportId,
        Guid adminId,
        CancellationToken cancellationToken = default) =>
        reportService.GetByIdAsync(reportId, adminId, UserRole.Admin, cancellationToken);

    public async Task<Result> ApproveAsync(
        Guid reportId,
        Guid adminId,
        CancellationToken cancellationToken = default)
    {
        var report = await dbContext.Reports
            .SingleOrDefaultAsync(report => report.Id == reportId, cancellationToken);

        if (report is null)
        {
            return ResultError.NotFound("Report not found.");
        }

        if (report.Status != ReportStatus.PendingReview)
        {
            return ResultError.Conflict("Only pending reports can be approved.");
        }

        var now = timeProvider.GetUtcNow();
        report.Status = ReportStatus.Published;
        reportLifecycleService.InitializePublishedTimer(report, now);
        report.UpdatedAt = now;

        dbContext.ModerationActions.Add(new ModerationAction
        {
            ReportId = report.Id,
            AdminId = adminId,
            Decision = ModerationDecision.Approve,
            CreatedAt = now,
        });

        dbContext.Notifications.Add(new Notification
        {
            Id = Guid.NewGuid(),
            UserId = report.ReporterId,
            Type = NotificationTypes.ReportApproved,
            PayloadJson = new NotificationPayload(
                NotificationTypes.ReportApproved,
                now,
                DeepLink: $"/my/reports/{report.Id}",
                ReportId: report.Id).ToJson(),
            IsRead = false,
            CreatedAt = now,
        });

        await dbContext.SaveChangesAsync(cancellationToken);
        return Result.Ok();
    }

    public async Task<Result> RejectAsync(
        Guid reportId,
        Guid adminId,
        RejectReportRequest request,
        CancellationToken cancellationToken = default)
    {
        var report = await dbContext.Reports
            .SingleOrDefaultAsync(report => report.Id == reportId, cancellationToken);

        if (report is null)
        {
            return ResultError.NotFound("Report not found.");
        }

        if (report.Status != ReportStatus.PendingReview)
        {
            return ResultError.Conflict("Only pending reports can be rejected.");
        }

        var now = timeProvider.GetUtcNow();
        report.Status = ReportStatus.Rejected;
        report.UpdatedAt = now;

        dbContext.ModerationActions.Add(new ModerationAction
        {
            ReportId = report.Id,
            AdminId = adminId,
            Decision = ModerationDecision.Reject,
            ReasonCode = request.ReasonCode,
            Note = request.Note,
            CreatedAt = now,
        });

        dbContext.Notifications.Add(new Notification
        {
            Id = Guid.NewGuid(),
            UserId = report.ReporterId,
            Type = NotificationTypes.ReportRejected,
            PayloadJson = new NotificationPayload(
                NotificationTypes.ReportRejected,
                now,
                DeepLink: $"/my/reports/{report.Id}",
                ReportId: report.Id,
                ReasonCode: request.ReasonCode,
                Note: request.Note).ToJson(),
            IsRead = false,
            CreatedAt = now,
        });

        await dbContext.SaveChangesAsync(cancellationToken);
        return Result.Ok();
    }

    private static ModerationQueueItemResponse ToQueueItem(Report report) =>
        new()
        {
            Id = report.Id,
            Type = ToReportType(report.Type),
            Title = report.Title,
            CategoryCode = report.Category.Code,
            Status = ToReportStatus(report.Status),
            CreatedAt = report.CreatedAt,
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
