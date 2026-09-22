using Amanah.Api.Data;
using Amanah.Api.Data.Entities;
using Amanah.Api.Hubs;
using Amanah.Api.Models.Errors;
using Amanah.Api.Services.Lifecycle;
using Amanah.Api.Services.Notifications;
using Amanah.Api.Utilities.Notifications;
using Amanah.Contracts.Errors;
using Amanah.Contracts.Requests.Admin;
using Amanah.Contracts.Responses.Admin;
using Amanah.Contracts.Chats;
using Amanah.Contracts.Responses.Chats;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace Amanah.Api.Services.Enforcement;

public sealed class AdminTakedownService(
    AppDbContext dbContext,
    ReportLifecycleService reportLifecycleService,
    ApprovedClaimCancellation approvedClaimCancellation,
    TimeProvider timeProvider,
    IHubContext<ChatHub> hubContext)
{
    public async Task<Result<AdminReportTakedownResponse>> TakeDownAsync(
        Guid reportId,
        Guid adminId,
        AdminReportTakedownRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!await dbContext.Reports.AnyAsync(
                existingReport => existingReport.Id == reportId,
                cancellationToken))
        {
            return ResultError.NotFound("Report not found.");
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        var now = timeProvider.GetUtcNow();
        Guid? approvedClaimantId = null;
        ApprovedClaimCancellation.CancellationOutcome? cancellation = null;

        await reportLifecycleService.LockReportRowForUpdateAsync(reportId, cancellationToken);

        if (await dbContext.Reports.AnyAsync(
                existingReport =>
                    existingReport.Id == reportId
                    && existingReport.Status == ReportStatus.Published,
                cancellationToken))
        {
            await reportLifecycleService.ClosePendingClaimsForAdminTakedownAsync(
                reportId,
                cancellationToken);
        }

        var publishedTransitionRows = await dbContext.Reports
            .Where(existingReport =>
                existingReport.Id == reportId
                && existingReport.Status == ReportStatus.Published)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(existingReport => existingReport.Status, ReportStatus.RemovedByAdmin)
                    .SetProperty(existingReport => existingReport.UpdatedAt, now),
                cancellationToken);

        if (publishedTransitionRows == 0)
        {
            var report = await dbContext.Reports
                .Include(existingReport => existingReport.Photos)
                .SingleAsync(existingReport => existingReport.Id == reportId, cancellationToken);

            if (report.Status != ReportStatus.ClaimInProgress)
            {
                await transaction.RollbackAsync(cancellationToken);
                return ResultError.Conflict(
                    "Only published reports or reports with an approved claim can be taken down.",
                    ErrorCodes.EnforcementReportNotTakedownable);
            }

            var cancelResult = await approvedClaimCancellation.CancelForEnforcementAsync(
                report,
                cancellationToken);
            if (!cancelResult.IsSuccess)
            {
                await transaction.RollbackAsync(cancellationToken);
                return cancelResult.Error!;
            }

            cancellation = cancelResult.Value!;
            approvedClaimantId = cancellation.ClaimantId;

            var claimInProgressTransitionRows = await dbContext.Reports
                .Where(existingReport =>
                    existingReport.Id == reportId
                    && existingReport.Status == ReportStatus.ClaimInProgress)
                .ExecuteUpdateAsync(
                    setters => setters
                        .SetProperty(existingReport => existingReport.Status, ReportStatus.RemovedByAdmin)
                        .SetProperty(existingReport => existingReport.UpdatedAt, now),
                    cancellationToken);

            if (claimInProgressTransitionRows == 0)
            {
                await transaction.RollbackAsync(cancellationToken);
                return ResultError.Conflict(
                    "Only published reports or reports with an approved claim can be taken down.",
                    ErrorCodes.EnforcementReportNotTakedownable);
            }
        }

        await reportLifecycleService.FinalizeAdminTakedownPhotosAsync(reportId, cancellationToken);

        var reportForNotifications = await dbContext.Reports
            .AsNoTracking()
            .SingleAsync(existingReport => existingReport.Id == reportId, cancellationToken);

        dbContext.ModerationActions.Add(new ModerationAction
        {
            ReportId = reportId,
            AdminId = adminId,
            Decision = ModerationDecision.Takedown,
            Note = request.Note,
            CreatedAt = now,
        });

        EnqueueTakedownNotification(reportForNotifications, reportForNotifications.ReporterId, now);
        if (approvedClaimantId is Guid claimantId)
        {
            EnqueueTakedownNotification(
                reportForNotifications,
                claimantId,
                now,
                cancellation!.ClaimId,
                cancellation.ReadOnlyThread?.Id);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        if (cancellation?.ReadOnlyThread is { } readOnlyThread)
        {
            await hubContext.Clients
                .Group(ChatHubGroups.ForThread(readOnlyThread.Id))
                .SendAsync(
                    ChatHubEvents.ThreadReadOnly,
                    new ChatThreadReadOnlyResponse
                    {
                        ThreadId = readOnlyThread.Id,
                        ReadOnlyAt = cancellation.ReadOnlyAt,
                    },
                    cancellationToken);
        }

        return new AdminReportTakedownResponse
        {
            ReportId = reportId,
            Status = "removed_by_admin",
        };
    }

    private void EnqueueTakedownNotification(
        Report report,
        Guid userId,
        DateTimeOffset now,
        Guid? claimId = null,
        Guid? chatThreadId = null)
    {
        dbContext.Notifications.Add(new Notification
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Type = NotificationTypes.AdminTakedownAffectingYou,
            PayloadJson = new NotificationPayload(
                NotificationTypes.AdminTakedownAffectingYou,
                now,
                DeepLink: $"/my/reports/{report.Id}",
                ReportId: report.Id,
                ClaimId: claimId,
                ChatThreadId: chatThreadId).ToJson(),
            IsRead = false,
            CreatedAt = now,
        });
    }
}
