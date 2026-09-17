using Amanah.Api.Data;
using Amanah.Api.Data.Entities;
using Amanah.Api.Models.Errors;
using Amanah.Api.Services.Auth;
using Amanah.Api.Utilities.Auth;
using Amanah.Contracts.Errors;
using Amanah.Contracts.Responses.Account;
using Microsoft.EntityFrameworkCore;

namespace Amanah.Api.Services.Lifecycle;

public sealed class AccountDeletionService(
    AppDbContext dbContext,
    ReportLifecycleService reportLifecycleService,
    TokenService tokenService,
    TimeProvider timeProvider)
{
    public const string WithdrawReason = "_account_deletion_";

    private const string ClaimInProgressBlocker =
        "You have a report with a claim in progress. Cancel the claim before deleting your account.";

    private const string ApprovedClaimBlocker =
        "You have an approved claim in progress. Cancel the claim before deleting your account.";

    public async Task<Result<AccountDeletionStatusResponse>> GetDeletionStatusAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var user = await dbContext.Users
            .AsNoTracking()
            .SingleOrDefaultAsync(existingUser => existingUser.Id == userId, cancellationToken);

        if (user is null)
        {
            return ResultError.NotFound("User not found.");
        }

        var blockers = await GetBlockersAsync(userId, cancellationToken);

        return new AccountDeletionStatusResponse
        {
            CanDelete = user.DeletionRequestedAt is null && blockers.Count == 0,
            Blockers = blockers,
            DeletionRequestedAt = user.DeletionRequestedAt,
        };
    }

    public async Task<Result> DeleteAccountAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var user = await dbContext.Users
            .SingleOrDefaultAsync(existingUser => existingUser.Id == userId, cancellationToken);

        if (user is null)
        {
            return ResultError.NotFound("User not found.");
        }

        if (user.DeletionRequestedAt is not null)
        {
            return ResultError.Conflict(
                "Account deletion has already been requested.",
                ErrorCodes.AccountDeletionAlreadyRequested);
        }

        var blockers = await GetBlockersAsync(userId, cancellationToken);
        if (blockers.Count > 0)
        {
            return DeletionBlocked(blockers);
        }

        var now = timeProvider.GetUtcNow();

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        // Withdraw pending claims with a conditional UPDATE first. Blindly mutating a
        // tracked Pending snapshot can overwrite a concurrent Approve and leave
        // ClaimInProgress + Withdrawn (cancel/resolve both blocked) — same class of
        // bug as the claim-timeout job (#25).
        await WithdrawPendingClaimsAsync(userId, now, cancellationToken);

        // Re-check after the claim update: a claim approved during deletion must block
        // us before we withdraw reports (R2 deletes) or mark the account deleted.
        blockers = await GetBlockersAsync(userId, cancellationToken);
        if (blockers.Count > 0)
        {
            return DeletionBlocked(blockers);
        }

        var reportIds = await dbContext.Reports
            .Where(report =>
                report.ReporterId == userId
                && (report.Status == ReportStatus.PendingReview
                    || report.Status == ReportStatus.Published))
            .Select(report => report.Id)
            .ToListAsync(cancellationToken);

        foreach (var reportId in reportIds)
        {
            var report = await dbContext.Reports
                .Include(existingReport => existingReport.Photos)
                .SingleAsync(existingReport => existingReport.Id == reportId, cancellationToken);

            // Concurrent approve can move the report to ClaimInProgress after the
            // candidate id snapshot. Abort rather than withdrawing with a stale status.
            if (report.Status == ReportStatus.ClaimInProgress)
            {
                return DeletionBlocked([ClaimInProgressBlocker]);
            }

            if (report.Status is not ReportStatus.PendingReview and not ReportStatus.Published)
            {
                continue;
            }

            var withdrawResult = await reportLifecycleService.WithdrawAsync(
                report,
                WithdrawReason,
                cancellationToken);

            if (!withdrawResult.IsSuccess)
            {
                return withdrawResult;
            }
        }

        blockers = await GetBlockersAsync(userId, cancellationToken);
        if (blockers.Count > 0)
        {
            return DeletionBlocked(blockers);
        }

        await dbContext.Messages
            .Where(message => message.SenderId == userId)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(message => message.SenderId, AnonymizedUser.Id),
                cancellationToken);

        user.DeletionRequestedAt = now;
        user.SenderAnonymizedAt = now;

        await tokenService.RevokeAllRefreshTokensAsync(userId, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return Result.Ok();
    }

    private async Task<IReadOnlyList<string>> GetBlockersAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        var blockers = new List<string>();

        var hasClaimInProgressReport = await dbContext.Reports
            .AsNoTracking()
            .AnyAsync(
                report =>
                    report.ReporterId == userId
                    && report.Status == ReportStatus.ClaimInProgress,
                cancellationToken);

        if (hasClaimInProgressReport)
        {
            blockers.Add(ClaimInProgressBlocker);
        }

        var hasApprovedClaim = await dbContext.Claims
            .AsNoTracking()
            .AnyAsync(
                claim =>
                    claim.ClaimantId == userId
                    && claim.Status == ClaimStatus.Approved,
                cancellationToken);

        if (hasApprovedClaim)
        {
            blockers.Add(ApprovedClaimBlocker);
        }

        return blockers;
    }

    private static ResultError DeletionBlocked(IReadOnlyList<string> blockers) =>
        new(
            ErrorCodes.AccountDeletionBlocked,
            "Account deletion is blocked.",
            StatusCodes.Status409Conflict,
            new Dictionary<string, string[]>
            {
                ["blockers"] = [.. blockers],
            });

    private async Task WithdrawPendingClaimsAsync(
        Guid userId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        // Conditional UPDATE so a concurrent Approve (Status=Approved) is never
        // overwritten by a stale Pending entity + SaveChanges.
        await dbContext.Claims
            .Where(claim => claim.ClaimantId == userId && claim.Status == ClaimStatus.Pending)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(claim => claim.Status, ClaimStatus.Withdrawn)
                    .SetProperty(claim => claim.ReviewedAt, now)
                    .SetProperty(claim => claim.CountsAsFailure, false),
                cancellationToken);
    }
}
