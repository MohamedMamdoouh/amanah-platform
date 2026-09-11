using Amanah.Api.Data;
using Amanah.Api.Data.Entities;
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
            .Include(claim => claim.Report)
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

            dbContext.Notifications.Add(new Notification
            {
                Id = Guid.NewGuid(),
                UserId = claim.ClaimantId,
                Type = NotificationTypes.ClaimClosedReportUnavailable,
                PayloadJson = new NotificationPayload(
                    NotificationTypes.ClaimClosedReportUnavailable,
                    now,
                    DeepLink: BuildReportDeepLink(report),
                    ReportId: report.Id,
                    ReasonCode: reason).ToJson(),
                IsRead = false,
                CreatedAt = now,
            });
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return pendingClaims.Count;
    }

    private static string BuildReportDeepLink(Report report) => report.Type switch
    {
        ReportType.Lost => $"/lost/{report.Id}",
        ReportType.Found => $"/found/{report.Id}",
        _ => $"/reports/{report.Id}",
    };
}
