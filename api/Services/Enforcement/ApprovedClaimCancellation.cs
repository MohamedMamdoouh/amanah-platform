using Amanah.Api.Data;
using Amanah.Api.Data.Entities;
using Amanah.Api.Models.Errors;
using Amanah.Api.Services.Claims;
using Amanah.Contracts.Errors;
using Microsoft.EntityFrameworkCore;

namespace Amanah.Api.Services.Enforcement;

public sealed class ApprovedClaimCancellation(
    AppDbContext dbContext,
    ClaimCleanupService claimCleanupService,
    TimeProvider timeProvider)
{
    public sealed record CancellationOutcome(
        Guid ClaimId,
        Guid ClaimantId,
        ChatThread? ReadOnlyThread,
        DateTimeOffset ReadOnlyAt);

    public async Task<Result<CancellationOutcome>> CancelForEnforcementAsync(
        Report report,
        CancellationToken cancellationToken = default)
    {
        if (report.Status != ReportStatus.ClaimInProgress)
        {
            return ResultError.Conflict(
                "This report does not have an approved claim to cancel.",
                ErrorCodes.EnforcementReportNotTakedownable);
        }

        var claim = await dbContext.Claims
            .Include(existingClaim => existingClaim.ChatThread)
            .Include(existingClaim => existingClaim.Report)
            .ThenInclude(existingReport => existingReport.Resolution)
            .Where(existingClaim =>
                existingClaim.ReportId == report.Id
                && existingClaim.Status == ClaimStatus.Approved)
            .SingleOrDefaultAsync(cancellationToken);

        if (claim is null)
        {
            return ResultError.Conflict(
                "This report does not have an approved claim to cancel.",
                ErrorCodes.EnforcementReportNotTakedownable);
        }

        var now = timeProvider.GetUtcNow();

        claim.Status = ClaimStatus.Cancelled;
        claim.CancelledByUserId = null;
        claim.CountsAsFailure = false;

        // Delete by report id so a concurrent confirm that committed after Include
        // cannot leave a stale Resolution on the taken-down report.
        var resolutionToRemove = claim.Report.Resolution
            ?? await dbContext.Resolutions
                .SingleOrDefaultAsync(
                    existingResolution => existingResolution.ReportId == report.Id,
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

        var photoKeys = claimCleanupService.ClearClaimPhoto(claim);
        await claimCleanupService.EnqueueClaimPhotoStorageAsync(photoKeys, cancellationToken);

        return new CancellationOutcome(claim.Id, claim.ClaimantId, claim.ChatThread, now);
    }
}
