using Amanah.Api.Data.Entities;
using Amanah.Api.Services.Lifecycle;
using Amanah.Api.Tests.Infrastructure;
using Amanah.Api.Tests.Reports;
using Amanah.Api.Utilities.Notifications;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Amanah.Api.Tests.Claims;

public class ClaimCleanupServiceTests(ApiWebApplicationFactory factory) : IClassFixture<ApiWebApplicationFactory>
{
    [Fact]
    public async Task WithdrawAsync_closes_only_pending_claims_without_consuming_attempts()
    {
        await using var context = await ReportTestContext.CreateAsync(factory);
        var reportId = await ClaimTestHelpers.PublishLostReportAsync(context);

        var firstClaimant = await ClaimTestHelpers.CreateAndLoginClaimantAsync(context);
        var secondClaimant = await ClaimTestHelpers.CreateAndLoginClaimantAsync(context);
        var thirdClaimant = await ClaimTestHelpers.CreateAndLoginClaimantAsync(context);

        var pendingClaimId = await ClaimTestHelpers.SeedPendingClaimAsync(
            context,
            reportId,
            firstClaimant.User.Id);
        var approvedClaimId = await ClaimTestHelpers.SeedApprovedClaimAsync(
            context,
            reportId,
            secondClaimant.User.Id);
        await ClaimTestHelpers.SeedRejectedClaimAsync(
            context,
            reportId,
            thirdClaimant.User.Id,
            countsAsFailure: true,
            attemptNumber: 1);

        await using var serviceScope = factory.Services.CreateAsyncScope();
        var lifecycleService = serviceScope.ServiceProvider.GetRequiredService<ReportLifecycleService>();
        var dbContext = serviceScope.ServiceProvider.GetRequiredService<Amanah.Api.Data.AppDbContext>();
        var report = await dbContext.Reports.SingleAsync(item => item.Id == reportId);

        const string reason = "Report withdrawn";
        var result = await lifecycleService.WithdrawAsync(report, reason);

        Assert.True(result.IsSuccess);

        var pendingClaim = await context.DbContext.Claims
            .AsNoTracking()
            .SingleAsync(claim => claim.Id == pendingClaimId);
        Assert.Equal(ClaimStatus.Withdrawn, pendingClaim.Status);
        Assert.Equal(ReportLifecycleService.ClosedReviewerDecision, pendingClaim.ReviewerDecision);
        Assert.Equal(reason, pendingClaim.DecisionReason);
        Assert.False(pendingClaim.CountsAsFailure);
        Assert.NotNull(pendingClaim.ReviewedAt);

        var approvedClaim = await context.DbContext.Claims
            .AsNoTracking()
            .SingleAsync(claim => claim.Id == approvedClaimId);
        Assert.Equal(ClaimStatus.Approved, approvedClaim.Status);

        var rejectedClaim = await context.DbContext.Claims
            .AsNoTracking()
            .SingleAsync(claim =>
                claim.ReportId == reportId
                && claim.ClaimantId == thirdClaimant.User.Id);
        Assert.Equal(ClaimStatus.Rejected, rejectedClaim.Status);
        Assert.True(rejectedClaim.CountsAsFailure);
    }

    [Fact]
    public async Task WithdrawAsync_notifies_each_affected_claimant_with_pending_claims()
    {
        await using var context = await ReportTestContext.CreateAsync(factory);
        var reportId = await ClaimTestHelpers.PublishLostReportAsync(context);

        var firstClaimant = await ClaimTestHelpers.CreateAndLoginClaimantAsync(context);
        var secondClaimant = await ClaimTestHelpers.CreateAndLoginClaimantAsync(context);

        await ClaimTestHelpers.SeedPendingClaimAsync(context, reportId, firstClaimant.User.Id, attemptNumber: 1);
        await ClaimTestHelpers.SeedPendingClaimAsync(context, reportId, secondClaimant.User.Id, attemptNumber: 1);

        await using var serviceScope = factory.Services.CreateAsyncScope();
        var lifecycleService = serviceScope.ServiceProvider.GetRequiredService<ReportLifecycleService>();
        var dbContext = serviceScope.ServiceProvider.GetRequiredService<Amanah.Api.Data.AppDbContext>();
        var report = await dbContext.Reports.SingleAsync(item => item.Id == reportId);

        const string reason = "_expired_";
        var result = await lifecycleService.WithdrawAsync(report, reason);

        Assert.True(result.IsSuccess);

        var notifications = await context.DbContext.Notifications
            .AsNoTracking()
            .Where(notification => notification.Type == NotificationTypes.ClaimClosedReportUnavailable)
            .ToListAsync();

        Assert.Equal(2, notifications.Count);
        Assert.Contains(notifications, notification => notification.UserId == firstClaimant.User.Id);
        Assert.Contains(notifications, notification => notification.UserId == secondClaimant.User.Id);
        Assert.All(
            notifications,
            notification =>
            {
                Assert.Contains($"/lost/{reportId}", notification.PayloadJson, StringComparison.Ordinal);
                Assert.Contains(reason, notification.PayloadJson, StringComparison.Ordinal);
            });
    }

    [Fact]
    public async Task WithdrawAsync_does_not_notify_when_no_pending_claims()
    {
        await using var context = await ReportTestContext.CreateAsync(factory);
        var reportId = await ClaimTestHelpers.PublishLostReportAsync(context);

        var claimant = await ClaimTestHelpers.CreateAndLoginClaimantAsync(context);
        await ClaimTestHelpers.SeedApprovedClaimAsync(context, reportId, claimant.User.Id);

        await using var serviceScope = factory.Services.CreateAsyncScope();
        var lifecycleService = serviceScope.ServiceProvider.GetRequiredService<ReportLifecycleService>();
        var dbContext = serviceScope.ServiceProvider.GetRequiredService<Amanah.Api.Data.AppDbContext>();
        var report = await dbContext.Reports.SingleAsync(item => item.Id == reportId);

        var result = await lifecycleService.WithdrawAsync(report, "Report withdrawn");

        Assert.True(result.IsSuccess);

        var notificationCount = await context.DbContext.Notifications
            .AsNoTracking()
            .CountAsync(notification =>
                notification.Type == NotificationTypes.ClaimClosedReportUnavailable);
        Assert.Equal(0, notificationCount);
    }

    [Fact]
    public async Task WithdrawAsync_does_not_affect_claims_on_other_reports()
    {
        await using var context = await ReportTestContext.CreateAsync(factory);
        var targetReportId = await ClaimTestHelpers.PublishLostReportAsync(context);
        var otherReportId = await ClaimTestHelpers.PublishFoundReportAsync(context);

        var claimant = await ClaimTestHelpers.CreateAndLoginClaimantAsync(context);
        var targetClaimId = await ClaimTestHelpers.SeedPendingClaimAsync(
            context,
            targetReportId,
            claimant.User.Id);
        var otherClaimId = await ClaimTestHelpers.SeedPendingClaimAsync(
            context,
            otherReportId,
            claimant.User.Id,
            attemptNumber: 2);

        await using var serviceScope = factory.Services.CreateAsyncScope();
        var lifecycleService = serviceScope.ServiceProvider.GetRequiredService<ReportLifecycleService>();
        var dbContext = serviceScope.ServiceProvider.GetRequiredService<Amanah.Api.Data.AppDbContext>();
        var report = await dbContext.Reports.SingleAsync(item => item.Id == targetReportId);

        var result = await lifecycleService.WithdrawAsync(report, "Report withdrawn");

        Assert.True(result.IsSuccess);

        var targetClaim = await context.DbContext.Claims
            .AsNoTracking()
            .SingleAsync(claim => claim.Id == targetClaimId);
        Assert.Equal(ClaimStatus.Withdrawn, targetClaim.Status);

        var otherClaim = await context.DbContext.Claims
            .AsNoTracking()
            .SingleAsync(claim => claim.Id == otherClaimId);
        Assert.Equal(ClaimStatus.Pending, otherClaim.Status);
    }
}
