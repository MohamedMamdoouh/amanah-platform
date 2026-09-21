using Amanah.Api.Data;
using Amanah.Api.Data.Entities;
using Amanah.Api.Hubs;
using Amanah.Api.Models.Errors;
using Amanah.Api.Services.Claims;
using Amanah.Api.Services.Notifications;
using Amanah.Api.Utilities.Notifications;
using Amanah.Contracts.Chats;
using Amanah.Contracts.Errors;
using Amanah.Contracts.Responses.Chats;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace Amanah.Api.Services.Resolution;

public sealed class ResolutionService(
    AppDbContext dbContext,
    ClaimCleanupService claimCleanupService,
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

        var isReporter = claim.Report.ReporterId == userId;
        var isClaimant = claim.ClaimantId == userId;
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

            await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

            var reportTransitionRows = await dbContext.Reports
                .Where(existingReport =>
                    existingReport.Id == claim.ReportId
                    && existingReport.Status == ReportStatus.ClaimInProgress)
                .ExecuteUpdateAsync(
                    setters => setters
                        .SetProperty(existingReport => existingReport.Status, ReportStatus.Resolved)
                        .SetProperty(existingReport => existingReport.UpdatedAt, now),
                    cancellationToken);

            if (reportTransitionRows == 0)
            {
                await transaction.RollbackAsync(cancellationToken);
                return ResultError.Conflict(
                    "Only claims on in-progress reports can be confirmed.",
                    ErrorCodes.ClaimInvalidStatus);
            }

            if (claim.ChatThread is not null)
            {
                claim.ChatThread.ReadOnlyAt = now;
                readOnlyThread = claim.ChatThread;
            }

            var photoKeys = claimCleanupService.ClearClaimPhoto(claim);

            var deepLink = claim.Report.Type switch
            {
                ReportType.Lost => $"/lost/{claim.Report.Id}",
                ReportType.Found => $"/found/{claim.Report.Id}",
                _ => $"/reports/{claim.Report.Id}",
            };

            dbContext.Notifications.Add(new Notification
            {
                Id = Guid.NewGuid(),
                UserId = claim.Report.ReporterId,
                Type = NotificationTypes.ReportResolved,
                PayloadJson = new NotificationPayload(
                    NotificationTypes.ReportResolved,
                    now,
                    DeepLink: deepLink,
                    ReportId: claim.ReportId,
                    ClaimId: claim.Id,
                    ChatThreadId: claim.ChatThread?.Id).ToJson(),
                IsRead = false,
                CreatedAt = now,
            });

            dbContext.Notifications.Add(new Notification
            {
                Id = Guid.NewGuid(),
                UserId = claim.ClaimantId,
                Type = NotificationTypes.ReportResolved,
                PayloadJson = new NotificationPayload(
                    NotificationTypes.ReportResolved,
                    now,
                    DeepLink: deepLink,
                    ReportId: claim.ReportId,
                    ClaimId: claim.Id,
                    ChatThreadId: claim.ChatThread?.Id).ToJson(),
                IsRead = false,
                CreatedAt = now,
            });

            await claimCleanupService.EnqueueClaimPhotoStorageAsync(photoKeys, cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        else
        {
            var counterpartyId = isReporter ? claim.ClaimantId : claim.Report.ReporterId;
            dbContext.Notifications.Add(new Notification
            {
                Id = Guid.NewGuid(),
                UserId = counterpartyId,
                Type = NotificationTypes.CounterpartyConfirmedResolution,
                PayloadJson = new NotificationPayload(
                    NotificationTypes.CounterpartyConfirmedResolution,
                    now,
                    DeepLink: claim.Report.Type switch
                    {
                        ReportType.Lost => $"/lost/{claim.Report.Id}",
                        ReportType.Found => $"/found/{claim.Report.Id}",
                        _ => $"/reports/{claim.Report.Id}",
                    },
                    ReportId: claim.ReportId,
                    ClaimId: claim.Id,
                    ChatThreadId: claim.ChatThread?.Id).ToJson(),
                IsRead = false,
                CreatedAt = now,
            });

            await dbContext.SaveChangesAsync(cancellationToken);
        }

        if (readOnlyThread is not null)
        {
            await hubContext.Clients
                .Group(ChatHubGroups.ForThread(readOnlyThread.Id))
                .SendAsync(
                    ChatHubEvents.ThreadReadOnly,
                    new ChatThreadReadOnlyResponse
                    {
                        ThreadId = readOnlyThread.Id,
                        ReadOnlyAt = readOnlyThread.ReadOnlyAt!.Value,
                    },
                    cancellationToken);
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

        var isReporter = claim.Report.ReporterId == userId;
        var isClaimant = claim.ClaimantId == userId;
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

        if (claim.Report.Status != ReportStatus.ClaimInProgress)
        {
            return ResultError.Conflict(
                "This claim can no longer be cancelled on the current report status.",
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
        var reportId = claim.ReportId;

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        var claimTransitionRows = await dbContext.Claims
            .Where(existingClaim =>
                existingClaim.Id == claimId
                && existingClaim.Status == ClaimStatus.Approved)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(existingClaim => existingClaim.Status, ClaimStatus.Cancelled)
                    .SetProperty(existingClaim => existingClaim.CancelledByUserId, userId)
                    .SetProperty(existingClaim => existingClaim.CountsAsFailure, isClaimant),
                cancellationToken);

        if (claimTransitionRows == 0)
        {
            await transaction.RollbackAsync(cancellationToken);
            return ResultError.Conflict(
                "Only approved claims can be cancelled.",
                ErrorCodes.ClaimInvalidStatus);
        }

        var reportTransitionRows = await dbContext.Reports
            .Where(existingReport =>
                existingReport.Id == reportId
                && existingReport.Status == ReportStatus.ClaimInProgress)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(existingReport => existingReport.Status, ReportStatus.Published)
                    .SetProperty(existingReport => existingReport.UpdatedAt, now)
                    .SetProperty(existingReport => existingReport.PublishedTimerResumedAt, now),
                cancellationToken);

        if (reportTransitionRows == 0)
        {
            await transaction.RollbackAsync(cancellationToken);
            return ResultError.Conflict(
                "This claim can no longer be cancelled on the current report status.",
                ErrorCodes.ClaimInvalidStatus);
        }

        dbContext.ChangeTracker.Clear();

        claim = await LoadClaimAsync(claimId, cancellationToken);
        if (claim is null)
        {
            await transaction.RollbackAsync(cancellationToken);
            return ResultError.NotFound("Claim not found.");
        }

        // Always delete by report id so a concurrent confirm that committed after our
        // initial load cannot leave a stale Resolution for the next claim cycle.
        var resolutionToRemove = await dbContext.Resolutions
            .SingleOrDefaultAsync(
                existingResolution => existingResolution.ReportId == reportId,
                cancellationToken);
        if (resolutionToRemove is not null)
        {
            dbContext.Resolutions.Remove(resolutionToRemove);
        }

        ChatThread? readOnlyThread = null;
        if (claim.ChatThread is not null)
        {
            claim.ChatThread.ReadOnlyAt = now;
            readOnlyThread = claim.ChatThread;
        }

        var photoKeys = claimCleanupService.ClearClaimPhoto(claim);

        var counterpartyId = isReporter ? claim.ClaimantId : claim.Report.ReporterId;
        dbContext.Notifications.Add(new Notification
        {
            Id = Guid.NewGuid(),
            UserId = counterpartyId,
            Type = NotificationTypes.ClaimCancelledByCounterparty,
            PayloadJson = new NotificationPayload(
                NotificationTypes.ClaimCancelledByCounterparty,
                now,
                DeepLink: claim.Report.Type switch
                {
                    ReportType.Lost => $"/lost/{claim.Report.Id}",
                    ReportType.Found => $"/found/{claim.Report.Id}",
                    _ => $"/reports/{claim.Report.Id}",
                },
                ReportId: claim.ReportId,
                ClaimId: claim.Id,
                ChatThreadId: claim.ChatThread?.Id).ToJson(),
            IsRead = false,
            CreatedAt = now,
        });

        await claimCleanupService.EnqueueClaimPhotoStorageAsync(photoKeys, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        if (readOnlyThread is not null)
        {
            await hubContext.Clients
                .Group(ChatHubGroups.ForThread(readOnlyThread.Id))
                .SendAsync(
                    ChatHubEvents.ThreadReadOnly,
                    new ChatThreadReadOnlyResponse
                    {
                        ThreadId = readOnlyThread.Id,
                        ReadOnlyAt = readOnlyThread.ReadOnlyAt!.Value,
                    },
                    cancellationToken);
        }

        return Result.Ok();
    }

    private Task<Claim?> LoadClaimAsync(Guid claimId, CancellationToken cancellationToken) =>
        dbContext.Claims
            .Include(claim => claim.Report)
            .ThenInclude(report => report.Resolution)
            .Include(claim => claim.ChatThread)
            .SingleOrDefaultAsync(existingClaim => existingClaim.Id == claimId, cancellationToken);
}
