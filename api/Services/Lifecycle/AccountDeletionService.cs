using Amanah.Api.Data;
using Amanah.Api.Data.Entities;
using Amanah.Api.Models.Errors;
using Amanah.Api.Services.Auth;
using Amanah.Api.Services.Claims;
using Amanah.Api.Utilities.Auth;
using Amanah.Contracts.Errors;
using Amanah.Contracts.Responses.Account;
using Microsoft.EntityFrameworkCore;

namespace Amanah.Api.Services.Lifecycle;

public sealed class AccountDeletionService(
    AppDbContext dbContext,
    ReportLifecycleService reportLifecycleService,
    ClaimCleanupService claimCleanupService,
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
            return new ResultError(
                ErrorCodes.AccountDeletionBlocked,
                "Account deletion is blocked.",
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
            var withdrawResult = await reportLifecycleService.WithdrawAsync(
                report,
                WithdrawReason,
                cancellationToken);

            if (!withdrawResult.IsSuccess)
            {
                return withdrawResult;
            }
        }

        await WithdrawPendingClaimsAsync(userId, now, cancellationToken);

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
            // SPEC §12: claim photos are deleted when the claim reaches Withdrawn.
            photoKeys.AddRange(claimCleanupService.ClearClaimPhoto(claim));
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await claimCleanupService.DeleteClaimPhotoStorageAsync(photoKeys, cancellationToken);
    }
}
