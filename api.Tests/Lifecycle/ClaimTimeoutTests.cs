using System.Net;
using Amanah.Api.Data.Entities;
using Amanah.Api.Tests.Claims;
using Amanah.Api.Tests.Infrastructure;
using Amanah.Api.Tests.Reports;
using Amanah.Api.Utilities.Notifications;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;

namespace Amanah.Api.Tests.Lifecycle;

public sealed class ClaimTimeoutWebApplicationFactory : ApiWebApplicationFactory
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.UseSetting("Lifecycle:ClaimTimeoutMinutes", "10");
    }
}

public class ClaimTimeoutTests(ClaimTimeoutWebApplicationFactory factory)
    : IClassFixture<ClaimTimeoutWebApplicationFactory>
{
    [Fact]
    public async Task PendingClaimTimeout_auto_withdraws_stale_claim_and_notifies_both_parties()
    {
        await using var context = await ReportTestContext.CreateAsync(factory);
        var reportId = await ClaimTestHelpers.PublishLostReportAsync(context);
        var claimant = await ClaimTestHelpers.CreateAndLoginClaimantAsync(context);
        var claimId = await ClaimTestHelpers.SeedPendingClaimAsync(context, reportId, claimant.User.Id);
        await BackdateSubmittedAtAsync(context, claimId, minutesAgo: 11);

        var response = await RunJobAsync(context, "PendingClaimTimeout");
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        var claim = await context.DbContext.Claims
            .AsNoTracking()
            .SingleAsync(item => item.Id == claimId);
        var report = await context.DbContext.Reports
            .AsNoTracking()
            .SingleAsync(item => item.Id == reportId);

        Assert.Equal(ClaimStatus.Withdrawn, claim.Status);
        Assert.False(claim.CountsAsFailure);
        Assert.NotNull(claim.ReviewedAt);
        Assert.Equal(ReportStatus.Published, report.Status);

        var notifications = await context.DbContext.Notifications
            .AsNoTracking()
            .Where(item => item.Type == NotificationTypes.ClaimAutoWithdrawn)
            .ToListAsync();

        Assert.Equal(2, notifications.Count);
        Assert.Contains(notifications, item => item.UserId == context.Session.User.Id);
        Assert.Contains(notifications, item => item.UserId == claimant.User.Id);
    }

    [Fact]
    public async Task PendingClaimTimeout_leaves_fresh_pending_claim_unchanged()
    {
        await using var context = await ReportTestContext.CreateAsync(factory);
        var reportId = await ClaimTestHelpers.PublishLostReportAsync(context);
        var claimant = await ClaimTestHelpers.CreateAndLoginClaimantAsync(context);
        var claimId = await ClaimTestHelpers.SeedPendingClaimAsync(context, reportId, claimant.User.Id);
        await BackdateSubmittedAtAsync(context, claimId, minutesAgo: 5);

        var response = await RunJobAsync(context, "PendingClaimTimeout");
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        var claim = await context.DbContext.Claims
            .AsNoTracking()
            .SingleAsync(item => item.Id == claimId);

        Assert.Equal(ClaimStatus.Pending, claim.Status);
        Assert.Null(claim.ReviewedAt);

        Assert.Equal(
            0,
            await context.DbContext.Notifications.CountAsync(
                item => item.Type == NotificationTypes.ClaimAutoWithdrawn));
    }

    [Fact]
    public async Task PendingClaimTimeout_does_not_consume_attempt()
    {
        await using var context = await ReportTestContext.CreateAsync(factory);
        var reportId = await ClaimTestHelpers.PublishLostReportAsync(context);
        var claimant = await ClaimTestHelpers.CreateAndLoginClaimantAsync(context);
        var claimId = await ClaimTestHelpers.SeedPendingClaimAsync(
            context,
            reportId,
            claimant.User.Id,
            attemptNumber: 2);
        await BackdateSubmittedAtAsync(context, claimId, minutesAgo: 11);

        var response = await RunJobAsync(context, "PendingClaimTimeout");
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        var claim = await context.DbContext.Claims
            .AsNoTracking()
            .SingleAsync(item => item.Id == claimId);

        Assert.Equal(ClaimStatus.Withdrawn, claim.Status);
        Assert.False(claim.CountsAsFailure);
        Assert.Equal(2, claim.AttemptNumber);

        var countedFailures = await context.DbContext.Claims
            .AsNoTracking()
            .CountAsync(item =>
                item.ReportId == reportId
                && item.ClaimantId == claimant.User.Id
                && item.CountsAsFailure);

        Assert.Equal(0, countedFailures);
    }

    private static async Task BackdateSubmittedAtAsync(
        ReportTestContext context,
        Guid claimId,
        int minutesAgo)
    {
        var claim = await context.DbContext.Claims.SingleAsync(item => item.Id == claimId);
        claim.SubmittedAt = DateTimeOffset.UtcNow.AddMinutes(-minutesAgo);
        await context.DbContext.SaveChangesAsync();
    }

    private static async Task<HttpResponseMessage> RunJobAsync(ReportTestContext context, string jobName)
    {
        await HttpTestHelpers.LoginAsAdminAsync(context);
        return await context.Client.PostAsync($"/api/v1/admin/test/run-job/{jobName}", null);
    }
}
