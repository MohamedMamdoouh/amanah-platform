using Amanah.Api.Data;
using Amanah.Api.Data.Entities;
using Amanah.Api.Models.Errors;
using Amanah.Api.Services.Notifications;
using Amanah.Api.Utilities.Notifications;
using Amanah.Contracts.Errors;
using Microsoft.EntityFrameworkCore;

namespace Amanah.Api.Services.Resolution;

public sealed class ResolutionService(AppDbContext dbContext, TimeProvider timeProvider)
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

        if (bothConfirmed)
        {
            resolution.ResolvedAt = now;
            claim.Report.Status = ReportStatus.Resolved;
            claim.Report.UpdatedAt = now;

            if (claim.ChatThread is not null)
            {
                claim.ChatThread.ReadOnlyAt = now;
            }

            var deepLink = BuildReportDeepLink(claim.Report);

            dbContext.Notifications.Add(CreateNotification(
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

            dbContext.Notifications.Add(CreateNotification(
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
            dbContext.Notifications.Add(CreateNotification(
                counterpartyId,
                NotificationTypes.CounterpartyConfirmedResolution,
                new NotificationPayload(
                    NotificationTypes.CounterpartyConfirmedResolution,
                    now,
                    DeepLink: BuildReportDeepLink(claim.Report),
                    ReportId: claim.ReportId,
                    ClaimId: claim.Id,
                    ChatThreadId: claim.ChatThread?.Id),
                now));
        }

        await dbContext.SaveChangesAsync(cancellationToken);
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

        if (claim.ChatThread is not null)
        {
            claim.ChatThread.ReadOnlyAt = now;
        }

        var counterpartyId = isReporter ? claim.ClaimantId : claim.Report.ReporterId;
        dbContext.Notifications.Add(CreateNotification(
            counterpartyId,
            NotificationTypes.ClaimCancelledByCounterparty,
            new NotificationPayload(
                NotificationTypes.ClaimCancelledByCounterparty,
                now,
                DeepLink: BuildReportDeepLink(claim.Report),
                ReportId: claim.ReportId,
                ClaimId: claim.Id,
                ChatThreadId: claim.ChatThread?.Id),
            now));

        await dbContext.SaveChangesAsync(cancellationToken);
        return Result.Ok();
    }

    private Task<Claim?> LoadClaimAsync(Guid claimId, CancellationToken cancellationToken) =>
        dbContext.Claims
            .Include(existingClaim => existingClaim.Report)
            .ThenInclude(report => report.Resolution)
            .Include(existingClaim => existingClaim.ChatThread)
            .SingleOrDefaultAsync(existingClaim => existingClaim.Id == claimId, cancellationToken);

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
}
