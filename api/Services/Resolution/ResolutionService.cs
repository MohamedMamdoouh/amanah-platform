using System.Data;
using Amanah.Api.Data;
using Amanah.Api.Data.Entities;
using Amanah.Api.Data.Extensions;
using Amanah.Api.Hubs;
using Amanah.Api.Models.Errors;
using Amanah.Api.Services.Notifications;
using Amanah.Api.Utilities.Claims;
using Amanah.Api.Utilities.Notifications;
using Amanah.Contracts.Chats;
using Amanah.Contracts.Errors;
using Amanah.Contracts.Responses.Chats;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Amanah.Api.Services.Resolution;

public sealed class ResolutionService(
    AppDbContext dbContext,
    TimeProvider timeProvider,
    IHubContext<ChatHub> hubContext)
{
    public async Task<Result> ConfirmResolutionAsync(
        Guid claimId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        // RepeatableRead + single load/check/write closes the confirm/cancel TOCTOU where a
        // stale Cancel could undo an already-committed mutual resolve (or leave
        // Report=Resolved with Claim=Cancelled).
        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            IsolationLevel.RepeatableRead,
            cancellationToken);

        try
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
            await transaction.CommitAsync(cancellationToken);

            if (readOnlyThread is not null)
            {
                await BroadcastThreadReadOnlyAsync(readOnlyThread, cancellationToken);
            }

            return Result.Ok();
        }
        catch (Exception ex) when (IsConcurrentUpdateConflict(ex))
        {
            return ResultError.Conflict(
                "This claim was updated concurrently. Please retry.",
                ErrorCodes.ClaimInvalidStatus);
        }
    }

    public async Task<Result> CancelAsync(
        Guid claimId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            IsolationLevel.RepeatableRead,
            cancellationToken);

        try
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

            if (claim.Report.Status != ReportStatus.ClaimInProgress)
            {
                return ResultError.Conflict(
                    "Only claims on in-progress reports can be cancelled.",
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
            await transaction.CommitAsync(cancellationToken);

            if (readOnlyThread is not null)
            {
                await BroadcastThreadReadOnlyAsync(readOnlyThread, cancellationToken);
            }

            return Result.Ok();
        }
        catch (Exception ex) when (IsConcurrentUpdateConflict(ex))
        {
            return ResultError.Conflict(
                "This claim was updated concurrently. Please retry.",
                ErrorCodes.ClaimInvalidStatus);
        }
    }

    private static bool IsConcurrentUpdateConflict(Exception exception)
    {
        for (var current = exception; current is not null; current = current.InnerException)
        {
            if (current is DbUpdateConcurrencyException)
            {
                return true;
            }

            if (current is PostgresException postgres
                && postgres.SqlState is PostgresErrorCodes.SerializationFailure
                    or PostgresErrorCodes.DeadlockDetected)
            {
                return true;
            }
        }

        return false;
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
            .WithResolutionDetailIncludes()
            .SingleOrDefaultAsync(existingClaim => existingClaim.Id == claimId, cancellationToken);
}
