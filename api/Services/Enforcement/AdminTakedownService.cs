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
        var report = await dbContext.Reports
            .Include(existingReport => existingReport.Photos)
            .SingleOrDefaultAsync(existingReport => existingReport.Id == reportId, cancellationToken);

        if (report is null)
        {
            return ResultError.NotFound("Report not found.");
        }

        if (report.Status is not ReportStatus.Published and not ReportStatus.ClaimInProgress)
        {
            return ResultError.Conflict(
                "Only published reports or reports with an approved claim can be taken down.",
                ErrorCodes.EnforcementReportNotTakedownable);
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        // Decision basis for the atomic status gate below. A concurrent Approve can move
        // Published → ClaimInProgress after this snapshot; blindly SaveChanges would then
        // overwrite ClaimInProgress with RemovedByAdmin while leaving the Approved claim
        // and an open chat (cancel/confirm stuck or cancel resurrecting the listing).
        var statusAtStart = report.Status;

        Guid? approvedClaimantId = null;
        ApprovedClaimCancellation.CancellationOutcome? cancellation = null;

        if (report.Status == ReportStatus.ClaimInProgress)
        {
            var cancelResult = await approvedClaimCancellation.CancelForEnforcementAsync(
                report,
                cancellationToken);
            if (!cancelResult.IsSuccess)
            {
                return cancelResult.Error!;
            }

            cancellation = cancelResult.Value!;
            approvedClaimantId = cancellation.ClaimantId;
        }

        await reportLifecycleService.ApplyAdminTakedownAsync(report, cancellationToken);

        var now = timeProvider.GetUtcNow();

        dbContext.ModerationActions.Add(new ModerationAction
        {
            ReportId = report.Id,
            AdminId = adminId,
            Decision = ModerationDecision.Takedown,
            Note = request.Note,
            CreatedAt = now,
        });

        EnqueueTakedownNotification(report, report.ReporterId, now);
        if (approvedClaimantId is Guid claimantId)
        {
            EnqueueTakedownNotification(report, claimantId, now, cancellation!.ClaimId, cancellation.ReadOnlyThread?.Id);
        }

        // Atomic gate: only commit if the report is still in the status we decided against.
        var statusUpdated = await dbContext.Reports
            .Where(existingReport =>
                existingReport.Id == report.Id
                && existingReport.Status == statusAtStart)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(existingReport => existingReport.Status, ReportStatus.RemovedByAdmin)
                    .SetProperty(existingReport => existingReport.UpdatedAt, now),
                cancellationToken);

        if (statusUpdated == 0)
        {
            await transaction.RollbackAsync(cancellationToken);
            dbContext.ChangeTracker.Clear();
            return ResultError.Conflict(
                "Only published reports or reports with an approved claim can be taken down.",
                ErrorCodes.EnforcementReportNotTakedownable);
        }

        // Status/UpdatedAt already written by ExecuteUpdate — avoid a second write from the tracker.
        dbContext.Entry(report).Property(existingReport => existingReport.Status).IsModified = false;
        dbContext.Entry(report).Property(existingReport => existingReport.UpdatedAt).IsModified = false;

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
            ReportId = report.Id,
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
