using Amanah.Api.Data;
using Amanah.Api.Data.Entities;
using Amanah.Api.Data.Extensions;
using Amanah.Api.Services.Notifications;
using Amanah.Api.Utilities.Notifications;
using Microsoft.EntityFrameworkCore;

namespace Amanah.Api.Services.Claims;

public sealed class ClaimCleanupService(
    AppDbContext dbContext,
    TimeProvider timeProvider) : IClaimCleanupService
{
    public const string ClosedReviewerDecision = "closed";

    public async Task<int> ClosePendingClaimsAsync(
        Guid reportId,
        string reason,
        CancellationToken cancellationToken = default)
    {
        var pendingClaims = await dbContext.Claims
            .WithReportInclude()
            .Where(claim =>
                claim.ReportId == reportId
                && claim.Status == ClaimStatus.Pending)
            .ToListAsync(cancellationToken);

        if (pendingClaims.Count == 0)
        {
            return 0;
        }

        var now = timeProvider.GetUtcNow();
        var report = pendingClaims[0].Report;

        foreach (var claim in pendingClaims)
        {
            claim.Status = ClaimStatus.Withdrawn;
            claim.ReviewedAt = now;
            claim.ReviewerDecision = ClosedReviewerDecision;
            claim.DecisionReason = reason;
            claim.CountsAsFailure = false;

            dbContext.Notifications.Add(NotificationEntityBuilder.Create(
                claim.ClaimantId,
                NotificationTypes.ClaimClosedReportUnavailable,
                new NotificationPayload(
                    NotificationTypes.ClaimClosedReportUnavailable,
                    now,
                    DeepLink: ReportDeepLinkBuilder.ForPublicReport(report),
                    ReportId: report.Id,
                    ReasonCode: reason),
                now));
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return pendingClaims.Count;
    }
}
