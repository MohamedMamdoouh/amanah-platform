using Amanah.Api.Data;
using Amanah.Api.Data.Entities;
using Amanah.Api.Hubs;
using Amanah.Api.Models.Errors;
using Amanah.Api.Services.Lifecycle;
using Amanah.Api.Services.Notifications;
using Amanah.Api.Utilities.Claims;
using Amanah.Api.Utilities.Notifications;
using Amanah.Contracts.Chats;
using Amanah.Contracts.Errors;
using Amanah.Contracts.Responses.Chats;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace Amanah.Api.Services.Resolution;

public sealed class ResolutionService(
    AppDbContext dbContext,
    IReportLifecycleService reportLifecycleService,
    TimeProvider timeProvider,
    IHubContext<ChatHub> hubContext)
{
    public async Task<Result> ConfirmResolutionAsync(
        Guid claimId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var claim = await LoadClaimAsync(claimId, cancellationToken);
        if (claim is null)
        {
            return ResultError.NotFound("Claim not found.");
        }

        var isReporter = ClaimAccessAuthorization.IsReporter(claim, userId);
        var isClaimant = ClaimAccessAuthorization.IsClaimant(claim, userId);
        if (!isReporter && !isClaimant)
        {
            return ResultError.NotFound("Claim not found.");
        }

        if (claim.Status != ClaimStatus.Approved)
        {
            return ResultError.Conflict(
                "Only approved claims can be confirmed.",
                ErrorCodes.ClaimInvalidStatus);
        }

        if (claim.Report.Status != ReportStatus.ClaimInProgress)
        {
            return ResultError.Conflict(
                "Only claims on in-progress reports can be confirmed.",
                ErrorCodes.ClaimInvalidStatus);
        }

        var resolution = claim.Report.Resolution;
        if (isReporter && resolution?.ReporterConfirmedAt is not null)
        {
            return ResultError.Conflict(
                "You have already confirmed resolution.",
                ErrorCodes.ClaimInvalidStatus);
        }

        if (isClaimant && resolution?.ClaimantConfirmedAt is not null)
        {
            return ResultError.Conflict(
                "You have already confirmed resolution.",
                ErrorCodes.ClaimInvalidStatus);
        }

        var now = timeProvider.GetUtcNow();

        if (resolution is null)
        {
            resolution = new Data.Entities.Resolution
            {
                Id = Guid.NewGuid(),
                ReportId = claim.ReportId,
            };

            dbContext.Resolutions.Add(resolution);
            claim.Report.Resolution = resolution;
        }

        if (isReporter)
        {
            resolution.ReporterConfirmedAt = now;
        }
        else
        {
            resolution.ClaimantConfirmedAt = now;
        }

        var bothConfirmed = resolution.ReporterConfirmedAt is not null
            && resolution.ClaimantConfirmedAt is not null;

        ChatThread? readOnlyThread = null;

        if (bothConfirmed)
        {
            resolution.ResolvedAt = now;
            claim.Report.Status = ReportStatus.Resolved;
            claim.Report.UpdatedAt = now;

            if (claim.ChatThread is not null)
            {
                claim.ChatThread.ReadOnlyAt = now;
                readOnlyThread = claim.ChatThread;
            }

            var deepLink = ReportDeepLinkBuilder.ForPublicReport(claim.Report);

            dbContext.Notifications.Add(NotificationEntityBuilder.Create(
                claim.Report.ReporterId,
                NotificationTypes.ReportResolved,
                new NotificationPayload(
                    NotificationTypes.ReportResolved,
                    now,
                    DeepLink: deepLink,
                    ReportId: claim.ReportId,
                    ClaimId: claim.Id,
                    ChatThreadId: claim.ChatThread?.Id),
                now));

            dbContext.Notifications.Add(NotificationEntityBuilder.Create(
                claim.ClaimantId,
                NotificationTypes.ReportResolved,
                new NotificationPayload(
                    NotificationTypes.ReportResolved,
                    now,
                    DeepLink: deepLink,
                    ReportId: claim.ReportId,
                    ClaimId: claim.Id,
                    ChatThreadId: claim.ChatThread?.Id),
                now));
        }
        else
        {
            var counterpartyId = isReporter ? claim.ClaimantId : claim.Report.ReporterId;
            dbContext.Notifications.Add(NotificationEntityBuilder.Create(
                counterpartyId,
                NotificationTypes.CounterpartyConfirmedResolution,
                new NotificationPayload(
                    NotificationTypes.CounterpartyConfirmedResolution,
                    now,
                    DeepLink: ReportDeepLinkBuilder.ForPublicReport(claim.Report),
                    ReportId: claim.ReportId,
                    ClaimId: claim.Id,
                    ChatThreadId: claim.ChatThread?.Id),
                now));
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        if (readOnlyThread is not null)
        {
            await BroadcastThreadReadOnlyAsync(readOnlyThread, cancellationToken);
        }

        return Result.Ok();
    }

    public async Task<Result> CancelAsync(
        Guid claimId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var claim = await LoadClaimAsync(claimId, cancellationToken);
        if (claim is null)
        {
            return ResultError.NotFound("Claim not found.");
        }

        var isReporter = ClaimAccessAuthorization.IsReporter(claim, userId);
        var isClaimant = ClaimAccessAuthorization.IsClaimant(claim, userId);
        if (!isReporter && !isClaimant)
        {
            return ResultError.NotFound("Claim not found.");
        }

        if (claim.Status != ClaimStatus.Approved)
        {
            return ResultError.Conflict(
                "Only approved claims can be cancelled.",
                ErrorCodes.ClaimInvalidStatus);
        }

        var resolution = claim.Report.Resolution;
        if (isReporter && resolution?.ReporterConfirmedAt is not null)
        {
            return ResultError.Conflict(
                "You cannot cancel after confirming resolution.",
                ErrorCodes.ClaimInvalidStatus);
        }

        if (isClaimant && resolution?.ClaimantConfirmedAt is not null)
        {
            return ResultError.Conflict(
                "You cannot cancel after confirming resolution.",
                ErrorCodes.ClaimInvalidStatus);
        }

        var now = timeProvider.GetUtcNow();

        claim.Status = ClaimStatus.Cancelled;
        claim.CancelledByUserId = userId;
        if (isClaimant)
        {
            claim.CountsAsFailure = true;
        }

        claim.Report.Status = ReportStatus.Published;
        reportLifecycleService.ResumePublishedTimer(claim.Report, now);
        claim.Report.UpdatedAt = now;

        // Always delete by report id so a concurrent confirm that committed after our
        // initial load cannot leave a stale Resolution for the next claim cycle.
        var resolutionToRemove = resolution
            ?? await dbContext.Resolutions
                .SingleOrDefaultAsync(
                    existingResolution => existingResolution.ReportId == claim.ReportId,
                    cancellationToken);
        if (resolutionToRemove is not null)
        {
            dbContext.Resolutions.Remove(resolutionToRemove);
        }

        claim.Report.Resolution = null;

        ChatThread? readOnlyThread = null;
        if (claim.ChatThread is not null)
        {
            claim.ChatThread.ReadOnlyAt = now;
            readOnlyThread = claim.ChatThread;
        }

        var counterpartyId = isReporter ? claim.ClaimantId : claim.Report.ReporterId;
        dbContext.Notifications.Add(NotificationEntityBuilder.Create(
            counterpartyId,
            NotificationTypes.ClaimCancelledByCounterparty,
            new NotificationPayload(
                NotificationTypes.ClaimCancelledByCounterparty,
                now,
                DeepLink: ReportDeepLinkBuilder.ForPublicReport(claim.Report),
                ReportId: claim.ReportId,
                ClaimId: claim.Id,
                ChatThreadId: claim.ChatThread?.Id),
            now));

        await dbContext.SaveChangesAsync(cancellationToken);

        if (readOnlyThread is not null)
        {
            await BroadcastThreadReadOnlyAsync(readOnlyThread, cancellationToken);
        }

        return Result.Ok();
    }

    private async Task BroadcastThreadReadOnlyAsync(
        ChatThread thread,
        CancellationToken cancellationToken)
    {
        if (thread.ReadOnlyAt is null)
        {
            return;
        }

        var payload = new ChatThreadReadOnlyResponse
        {
            ThreadId = thread.Id,
            ReadOnlyAt = thread.ReadOnlyAt.Value,
        };

        await hubContext.Clients
            .Group(ChatHubGroups.ForThread(thread.Id))
            .SendAsync(ChatHubEvents.ThreadReadOnly, payload, cancellationToken);
    }

    private Task<Claim?> LoadClaimAsync(Guid claimId, CancellationToken cancellationToken) =>
        dbContext.Claims
            .Include(claim => claim.Report)
            .ThenInclude(report => report.Resolution)
            .Include(claim => claim.ChatThread)
            .SingleOrDefaultAsync(existingClaim => existingClaim.Id == claimId, cancellationToken);
}
