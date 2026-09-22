using Amanah.Api.Data;
using Amanah.Api.Data.Entities;
using Amanah.Api.Hubs;
using Amanah.Api.Models.Errors;
using Amanah.Api.Services.Auth;
using Amanah.Api.Services.Claims;
using Amanah.Api.Services.Lifecycle;
using Amanah.Api.Services.Notifications;
using Amanah.Api.Utilities.Notifications;
using Amanah.Contracts.Chats;
using Amanah.Contracts.Errors;
using Amanah.Contracts.Requests.Admin;
using Amanah.Contracts.Responses.Admin;
using Amanah.Contracts.Responses.Chats;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace Amanah.Api.Services.Enforcement;

public sealed class UserEnforcementService(
    AppDbContext dbContext,
    ReportLifecycleService reportLifecycleService,
    ApprovedClaimCancellation approvedClaimCancellation,
    ClaimCleanupService claimCleanupService,
    TokenService tokenService,
    TimeProvider timeProvider,
    IHubContext<ChatHub> hubContext)
{
    public async Task<Result<BanUserResponse>> BanAsync(
        Guid userId,
        Guid adminId,
        BanUserRequest request,
        CancellationToken cancellationToken = default)
    {
        var reason = request.Reason.Trim();
        if (string.IsNullOrEmpty(reason))
        {
            return ResultError.BadRequest("Ban reason is required.");
        }

        var user = await dbContext.Users
            .SingleOrDefaultAsync(existingUser => existingUser.Id == userId, cancellationToken);

        if (user is null)
        {
            return ResultError.NotFound("User not found.");
        }

        if (user.IsBanned)
        {
            return ResultError.Conflict(
                "This user is already banned.",
                ErrorCodes.EnforcementUserAlreadyBanned);
        }

        var now = timeProvider.GetUtcNow();
        var readOnlyEvents = new List<(Guid ThreadId, DateTimeOffset ReadOnlyAt)>();

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        // A claim stays Approved after both parties confirm, while the report becomes
        // Resolved (terminal). Only an in-progress report still has a claim to cancel.
        var approvedClaims = await dbContext.Claims
            .Include(claim => claim.Report)
            .ThenInclude(report => report.Photos)
            .Include(claim => claim.Report)
            .ThenInclude(report => report.Resolution)
            .Include(claim => claim.ChatThread)
            .Where(claim =>
                claim.Status == ClaimStatus.Approved
                && claim.Report.Status == ReportStatus.ClaimInProgress
                && (claim.ClaimantId == userId || claim.Report.ReporterId == userId))
            .ToListAsync(cancellationToken);

        foreach (var claim in approvedClaims)
        {
            await reportLifecycleService.LockReportRowForUpdateAsync(claim.Report.Id, cancellationToken);

            // Confirm can commit Resolved after the query above and before this lock.
            // Cancelling then would delete that resolution and reopen or withdraw the report.
            var lockedStatus = await dbContext.Reports
                .AsNoTracking()
                .Where(existingReport => existingReport.Id == claim.ReportId)
                .Select(existingReport => (ReportStatus?)existingReport.Status)
                .SingleOrDefaultAsync(cancellationToken);
            if (lockedStatus != ReportStatus.ClaimInProgress)
            {
                continue;
            }

            var cancelResult = await approvedClaimCancellation.CancelForEnforcementAsync(
                claim.Report,
                cancellationToken);
            if (!cancelResult.IsSuccess)
            {
                return cancelResult.Error!;
            }

            var cancellation = cancelResult.Value!;
            if (cancellation.ReadOnlyThread is not null)
            {
                readOnlyEvents.Add((cancellation.ReadOnlyThread.Id, cancellation.ReadOnlyAt));
            }

            var counterpartyId = claim.Report.ReporterId == userId
                ? claim.ClaimantId
                : claim.Report.ReporterId;
            EnqueueClaimEndedByEnforcementNotification(
                claim.Report,
                counterpartyId,
                cancellation.ClaimId,
                cancellation.ReadOnlyThread?.Id,
                now);

            if (claim.Report.ReporterId == userId)
            {
                var withdrawResult = await reportLifecycleService.WithdrawForBanCleanupAsync(
                    claim.Report,
                    cancellationToken);
                if (!withdrawResult.IsSuccess)
                {
                    return withdrawResult.Error!;
                }
            }
            else
            {
                claim.Report.Status = ReportStatus.Published;
                reportLifecycleService.ResumePublishedTimer(claim.Report, now);
                claim.Report.UpdatedAt = now;
            }
        }

        var activeReports = await dbContext.Reports
            .Include(report => report.Photos)
            .Where(report =>
                report.ReporterId == userId
                && (report.Status == ReportStatus.PendingReview
                    || report.Status == ReportStatus.Published))
            .ToListAsync(cancellationToken);

        foreach (var report in activeReports)
        {
            var withdrawResult = await reportLifecycleService.WithdrawForBanCleanupAsync(
                report,
                cancellationToken);
            if (!withdrawResult.IsSuccess)
            {
                return withdrawResult.Error!;
            }
        }

        await WithdrawPendingClaimsAsync(userId, now, cancellationToken);

        user.IsBanned = true;
        user.BanReason = reason;
        user.BannedAt = now;

        dbContext.ModerationActions.Add(new ModerationAction
        {
            AdminId = adminId,
            Decision = ModerationDecision.Ban,
            ReasonCode = userId.ToString(),
            Note = reason,
            CreatedAt = now,
        });

        await tokenService.RevokeAllRefreshTokensAsync(userId, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        foreach (var (threadId, readOnlyAt) in readOnlyEvents)
        {
            await hubContext.Clients
                .Group(ChatHubGroups.ForThread(threadId))
                .SendAsync(
                    ChatHubEvents.ThreadReadOnly,
                    new ChatThreadReadOnlyResponse
                    {
                        ThreadId = threadId,
                        ReadOnlyAt = readOnlyAt,
                    },
                    cancellationToken);
        }

        return new BanUserResponse
        {
            UserId = user.Id,
            BanReason = reason,
            BannedAt = now,
        };
    }

    public async Task<Result<UnbanUserResponse>> UnbanAsync(
        Guid userId,
        Guid adminId,
        CancellationToken cancellationToken = default)
    {
        var user = await dbContext.Users
            .SingleOrDefaultAsync(existingUser => existingUser.Id == userId, cancellationToken);

        if (user is null)
        {
            return ResultError.NotFound("User not found.");
        }

        if (!user.IsBanned)
        {
            return ResultError.Conflict(
                "This user is not banned.",
                ErrorCodes.EnforcementUserNotBanned);
        }

        var now = timeProvider.GetUtcNow();

        user.IsBanned = false;
        user.BanReason = null;
        user.BannedAt = null;

        dbContext.ModerationActions.Add(new ModerationAction
        {
            AdminId = adminId,
            Decision = ModerationDecision.Unban,
            ReasonCode = userId.ToString(),
            CreatedAt = now,
        });

        await dbContext.SaveChangesAsync(cancellationToken);

        return new UnbanUserResponse { UserId = user.Id };
    }

    private void EnqueueClaimEndedByEnforcementNotification(
        Report report,
        Guid counterpartyId,
        Guid claimId,
        Guid? chatThreadId,
        DateTimeOffset now)
    {
        dbContext.Notifications.Add(new Notification
        {
            Id = Guid.NewGuid(),
            UserId = counterpartyId,
            Type = NotificationTypes.ClaimEndedByEnforcement,
            PayloadJson = new NotificationPayload(
                NotificationTypes.ClaimEndedByEnforcement,
                now,
                DeepLink: report.Type switch
                {
                    ReportType.Lost => $"/lost/{report.Id}",
                    ReportType.Found => $"/found/{report.Id}",
                    _ => $"/reports/{report.Id}",
                },
                ReportId: report.Id,
                ClaimId: claimId,
                ChatThreadId: chatThreadId).ToJson(),
            IsRead = false,
            CreatedAt = now,
        });
    }

    private async Task WithdrawPendingClaimsAsync(
        Guid userId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var pendingClaims = await dbContext.Claims
            .Include(claim => claim.Report)
            .Where(claim => claim.ClaimantId == userId && claim.Status == ClaimStatus.Pending)
            .ToListAsync(cancellationToken);

        if (pendingClaims.Count == 0)
        {
            return;
        }

        var photoKeys = new List<string>();
        foreach (var claim in pendingClaims)
        {
            claim.Status = ClaimStatus.Withdrawn;
            claim.ReviewedAt = now;
            claim.CountsAsFailure = false;
            photoKeys.AddRange(claimCleanupService.ClearClaimPhoto(claim));

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
        }

        await claimCleanupService.EnqueueClaimPhotoStorageAsync(photoKeys, cancellationToken);
    }
}
