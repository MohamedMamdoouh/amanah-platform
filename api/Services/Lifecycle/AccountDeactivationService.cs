using Amanah.Api.Data;
using Amanah.Api.Data.Entities;
using Amanah.Api.Models.Errors;
using Amanah.Api.Services.Auth;
using Amanah.Api.Services.Claims;
using Amanah.Contracts.Errors;
using Amanah.Contracts.Responses.Account;
using Microsoft.EntityFrameworkCore;

namespace Amanah.Api.Services.Lifecycle;

public sealed class AccountDeactivationService(
    AppDbContext dbContext,
    ReportLifecycleService reportLifecycleService,
    ClaimCleanupService claimCleanupService,
    TokenService tokenService,
    TimeProvider timeProvider)
{
    public const string WithdrawReason = "_account_deactivation_";

    public async Task<Result<AccountDeactivationStatusResponse>> GetDeactivationStatusAsync(
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

        return new AccountDeactivationStatusResponse
        {
            CanDeactivate = user.DeactivatedAt is null && blockers.Count == 0,
            Blockers = blockers,
            DeactivatedAt = user.DeactivatedAt,
        };
    }

    public async Task<Result> DeactivateAccountAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var user = await dbContext.Users
            .SingleOrDefaultAsync(existingUser => existingUser.Id == userId, cancellationToken);

        if (user is null)
        {
            return ResultError.NotFound("User not found.");
        }

        if (user.DeactivatedAt is not null)
        {
            return ResultError.Conflict(
                "This account is already deactivated.",
                ErrorCodes.AccountDeactivationAlreadyRequested);
        }

        var blockers = await GetBlockersAsync(userId, cancellationToken);
        if (blockers.Count > 0)
        {
            return new ResultError(
                ErrorCodes.AccountDeactivationBlocked,
                "Account deactivation is blocked.",
                StatusCodes.Status409Conflict,
                new Dictionary<string, string[]>
                {
                    ["blockers"] = [.. blockers],
                });
        }

        var now = timeProvider.GetUtcNow();

        var reports = await dbContext.Reports
            .Include(report => report.Photos)
            .Where(report =>
                report.ReporterId == userId
                && (report.Status == ReportStatus.PendingReview
                    || report.Status == ReportStatus.Published))
            .ToListAsync(cancellationToken);

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        foreach (var report in reports)
        {
            var withdrawResult = await reportLifecycleService.WithdrawInTransactionAsync(
                report,
                WithdrawReason,
                cancellationToken);

            if (!withdrawResult.IsSuccess)
            {
                await transaction.RollbackAsync(cancellationToken);
                return withdrawResult;
            }
        }

        await WithdrawPendingClaimsAsync(userId, now, cancellationToken);

        user.DeactivatedAt = now;

        await tokenService.RevokeAllRefreshTokensAsync(userId, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return Result.Ok();
    }

    public async Task<Result> ReactivateAccountAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var user = await dbContext.Users
            .SingleOrDefaultAsync(existingUser => existingUser.Id == userId, cancellationToken);

        if (user is null)
        {
            return ResultError.NotFound("User not found.");
        }

        if (user.DeactivatedAt is null)
        {
            return ResultError.Conflict(
                "This account is not deactivated.",
                ErrorCodes.Conflict);
        }

        user.DeactivatedAt = null;
        await dbContext.SaveChangesAsync(cancellationToken);

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
            blockers.Add(ErrorCodes.AccountBlockerClaimInProgress);
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
            blockers.Add(ErrorCodes.AccountBlockerApprovedClaim);
        }

        return blockers;
    }

    private async Task WithdrawPendingClaimsAsync(
        Guid userId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var pendingClaims = await dbContext.Claims
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
        }

        await claimCleanupService.EnqueueClaimPhotoStorageAsync(photoKeys, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
