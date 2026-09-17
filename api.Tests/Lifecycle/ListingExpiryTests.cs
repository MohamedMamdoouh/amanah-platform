using System.Net;
using System.Net.Http.Json;
using Amanah.Api.Data.Entities;
using Amanah.Api.Services.Lifecycle;
using Amanah.Api.Services.Notifications;
using Amanah.Api.Tests.Claims;
using Amanah.Api.Tests.Infrastructure;
using Amanah.Api.Tests.Reports;
using Amanah.Api.Utilities.Notifications;
using Amanah.Contracts.Requests.Admin;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;

namespace Amanah.Api.Tests.Lifecycle;

public sealed class ListingExpiryWebApplicationFactory : ApiWebApplicationFactory
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.UseSetting("Lifecycle:ListingExpiryDays", "2");
        builder.UseSetting("Lifecycle:ListingExpiryWarningDaysBefore", "1");
    }
}

public class ListingExpiryTests(ListingExpiryWebApplicationFactory factory)
    : IClassFixture<ListingExpiryWebApplicationFactory>
{
    private const int SecondsPerDay = 86_400;

    [Fact]
    public async Task ListingExpiryWarning_sends_notification_and_sets_flag_at_threshold()
    {
        await using var context = await ReportTestContext.CreateAsync(factory);
        var reportId = await ClaimTestHelpers.PublishLostReportAsync(context);
        await SetPublishedElapsedDaysAsync(context, reportId, days: 1);

        var response = await RunJobAsync(context, "ListingExpiryWarning");
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        var report = await context.DbContext.Reports
            .AsNoTracking()
            .SingleAsync(item => item.Id == reportId);
        Assert.True(report.ExpiryWarningSent);

        var notification = await context.DbContext.Notifications
            .AsNoTracking()
            .SingleAsync(item =>
                item.UserId == context.Session.User.Id
                && item.Type == NotificationTypes.ReportExpiringSoon);
        Assert.Equal(reportId, NotificationPayload.FromJson(notification.PayloadJson).ReportId);
    }

    [Fact]
    public async Task ListingExpiryWarning_skips_reports_below_threshold()
    {
        await using var context = await ReportTestContext.CreateAsync(factory);
        var reportId = await ClaimTestHelpers.PublishLostReportAsync(context);
        await SetPublishedElapsedDaysAsync(context, reportId, days: 0);

        var response = await RunJobAsync(context, "ListingExpiryWarning");
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        var report = await context.DbContext.Reports
            .AsNoTracking()
            .SingleAsync(item => item.Id == reportId);
        Assert.False(report.ExpiryWarningSent);

        Assert.Equal(
            0,
            await context.DbContext.Notifications.CountAsync(
                notification => notification.Type == NotificationTypes.ReportExpiringSoon));
    }

    [Fact]
    public async Task ListingExpiryWarning_skips_reports_that_already_received_warning()
    {
        await using var context = await ReportTestContext.CreateAsync(factory);
        var reportId = await ClaimTestHelpers.PublishLostReportAsync(context);
        await SetPublishedElapsedDaysAsync(context, reportId, days: 1);

        var report = await context.DbContext.Reports.SingleAsync(item => item.Id == reportId);
        report.ExpiryWarningSent = true;
        await context.DbContext.SaveChangesAsync();

        var response = await RunJobAsync(context, "ListingExpiryWarning");
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        Assert.Equal(
            0,
            await context.DbContext.Notifications.CountAsync(
                notification => notification.Type == NotificationTypes.ReportExpiringSoon));
    }

    [Fact]
    public async Task ListingExpiryWarning_does_not_touch_pending_review_or_rejected_reports()
    {
        await using var context = await ReportTestContext.CreateAsync(factory);
        var (_, pendingReview) = await context.SubmitReportAsync(TestReportHelpers.BuildValidLostRequest());
        Assert.NotNull(pendingReview);

        var (_, rejectedCandidate) = await context.SubmitReportAsync(TestReportHelpers.BuildValidLostRequest());
        Assert.NotNull(rejectedCandidate);

        await HttpTestHelpers.LoginAsAdminAsync(context);
        var rejectResponse = await context.Client.PostAsJsonAsync(
            $"/api/v1/admin/moderation/reports/{rejectedCandidate.Id}/reject",
            new RejectReportRequest
            {
                ReasonCode = "rejection.insufficient_description",
            });
        Assert.Equal(HttpStatusCode.NoContent, rejectResponse.StatusCode);

        var response = await RunJobAsync(context, "ListingExpiryWarning");
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        var pendingReport = await context.DbContext.Reports
            .AsNoTracking()
            .SingleAsync(item => item.Id == pendingReview.Id);
        var rejectedReport = await context.DbContext.Reports
            .AsNoTracking()
            .SingleAsync(item => item.Id == rejectedCandidate.Id);

        Assert.Equal(ReportStatus.PendingReview, pendingReport.Status);
        Assert.Equal(ReportStatus.Rejected, rejectedReport.Status);
        Assert.False(pendingReport.ExpiryWarningSent);
        Assert.False(rejectedReport.ExpiryWarningSent);
    }

    [Fact]
    public async Task ListingAutoExpiry_withdraws_report_with_expired_reason()
    {
        await using var context = await ReportTestContext.CreateAsync(factory);
        var reportId = await ClaimTestHelpers.PublishLostReportAsync(context);
        await SetPublishedElapsedDaysAsync(context, reportId, days: 2);

        var response = await RunJobAsync(context, "ListingAutoExpiry");
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        var report = await context.DbContext.Reports
            .AsNoTracking()
            .SingleAsync(item => item.Id == reportId);
        Assert.Equal(ReportStatus.Withdrawn, report.Status);
        Assert.Equal(ReportLifecycleService.ExpiredWithdrawReason, report.WithdrawalReason);
    }

    [Fact]
    public async Task ListingAutoExpiry_closes_pending_claims_and_notifies_reporter_and_claimants()
    {
        await using var context = await ReportTestContext.CreateAsync(factory);
        var reportId = await ClaimTestHelpers.PublishLostReportAsync(context);
        await SetPublishedElapsedDaysAsync(context, reportId, days: 2);

        var firstClaimant = await ClaimTestHelpers.CreateAndLoginClaimantAsync(context);
        var secondClaimant = await ClaimTestHelpers.CreateAndLoginClaimantAsync(context);
        var firstPendingClaimId = await ClaimTestHelpers.SeedPendingClaimAsync(
            context,
            reportId,
            firstClaimant.User.Id);
        var secondPendingClaimId = await ClaimTestHelpers.SeedPendingClaimAsync(
            context,
            reportId,
            secondClaimant.User.Id);

        var response = await RunJobAsync(context, "ListingAutoExpiry");
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        var firstPendingClaim = await context.DbContext.Claims
            .AsNoTracking()
            .SingleAsync(claim => claim.Id == firstPendingClaimId);
        var secondPendingClaim = await context.DbContext.Claims
            .AsNoTracking()
            .SingleAsync(claim => claim.Id == secondPendingClaimId);

        Assert.Equal(ClaimStatus.Withdrawn, firstPendingClaim.Status);
        Assert.Equal(ClaimStatus.Withdrawn, secondPendingClaim.Status);
        Assert.Equal(ReportLifecycleService.ClosedReviewerDecision, firstPendingClaim.ReviewerDecision);
        Assert.Equal(ReportLifecycleService.ExpiredWithdrawReason, firstPendingClaim.DecisionReason);
        Assert.False(firstPendingClaim.CountsAsFailure);

        var notifications = await context.DbContext.Notifications
            .AsNoTracking()
            .Where(notification => notification.Type == NotificationTypes.ReportExpired)
            .ToListAsync();

        Assert.Equal(3, notifications.Count);
        Assert.Contains(notifications, notification => notification.UserId == context.Session.User.Id);
        Assert.Contains(notifications, notification => notification.UserId == firstClaimant.User.Id);
        Assert.Contains(notifications, notification => notification.UserId == secondClaimant.User.Id);
    }

    [Fact]
    public async Task ListingAutoExpiry_skips_reports_below_threshold()
    {
        await using var context = await ReportTestContext.CreateAsync(factory);
        var reportId = await ClaimTestHelpers.PublishLostReportAsync(context);
        await SetPublishedElapsedDaysAsync(context, reportId, days: 1);

        var response = await RunJobAsync(context, "ListingAutoExpiry");
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        var report = await context.DbContext.Reports
            .AsNoTracking()
            .SingleAsync(item => item.Id == reportId);
        Assert.Equal(ReportStatus.Published, report.Status);
    }

    [Fact]
    public async Task ListingAutoExpiry_does_not_touch_pending_review_or_rejected_reports()
    {
        await using var context = await ReportTestContext.CreateAsync(factory);
        var (_, pendingReview) = await context.SubmitReportAsync(TestReportHelpers.BuildValidLostRequest());
        Assert.NotNull(pendingReview);

        var (_, rejectedCandidate) = await context.SubmitReportAsync(TestReportHelpers.BuildValidLostRequest());
        Assert.NotNull(rejectedCandidate);

        await HttpTestHelpers.LoginAsAdminAsync(context);
        var rejectResponse = await context.Client.PostAsJsonAsync(
            $"/api/v1/admin/moderation/reports/{rejectedCandidate.Id}/reject",
            new RejectReportRequest
            {
                ReasonCode = "rejection.insufficient_description",
            });
        Assert.Equal(HttpStatusCode.NoContent, rejectResponse.StatusCode);

        var response = await RunJobAsync(context, "ListingAutoExpiry");
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        var pendingReport = await context.DbContext.Reports
            .AsNoTracking()
            .SingleAsync(item => item.Id == pendingReview.Id);
        var rejectedReport = await context.DbContext.Reports
            .AsNoTracking()
            .SingleAsync(item => item.Id == rejectedCandidate.Id);

        Assert.Equal(ReportStatus.PendingReview, pendingReport.Status);
        Assert.Equal(ReportStatus.Rejected, rejectedReport.Status);
    }

    private static async Task SetPublishedElapsedDaysAsync(
        ReportTestContext context,
        Guid reportId,
        int days)
    {
        var report = await context.DbContext.Reports.SingleAsync(item => item.Id == reportId);
        report.PublishedSecondsElapsed = days * SecondsPerDay;
        report.PublishedTimerResumedAt = null;
        await context.DbContext.SaveChangesAsync();
    }

    private static async Task<HttpResponseMessage> RunJobAsync(ReportTestContext context, string jobName)
    {
        await HttpTestHelpers.LoginAsAdminAsync(context);
        return await context.Client.PostAsync($"/api/v1/admin/test/run-job/{jobName}", null);
    }
}
