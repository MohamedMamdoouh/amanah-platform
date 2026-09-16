using System.Net;
using System.Net.Http.Json;
using Amanah.Api.Data.Entities;
using Amanah.Api.Services.Lifecycle;
using Amanah.Api.Tests.Claims;
using Amanah.Api.Tests.Infrastructure;
using Amanah.Api.Tests.Reports;
using Amanah.Api.Tests.Resolution;
using Amanah.Contracts.Requests.Admin;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Amanah.Api.Tests.Lifecycle;

public class PublishedTimerTests(ApiWebApplicationFactory factory) : IClassFixture<ApiWebApplicationFactory>
{
    [Fact]
    public async Task Approve_report_initializes_published_timer()
    {
        await using var context = await ReportTestContext.CreateAsync(factory);
        var (_, created) = await context.SubmitReportAsync(TestReportHelpers.BuildValidLostRequest());
        Assert.NotNull(created);

        await HttpTestHelpers.LoginAsAdminAsync(context);

        var approveResponse = await context.Client.PostAsync(
            $"/api/v1/admin/moderation/reports/{created.Id}/approve",
            null);
        Assert.Equal(HttpStatusCode.NoContent, approveResponse.StatusCode);

        var report = await context.DbContext.Reports
            .AsNoTracking()
            .SingleAsync(item => item.Id == created.Id);

        Assert.Equal(ReportStatus.Published, report.Status);
        Assert.NotNull(report.PublishedAt);
        Assert.NotNull(report.PublishedTimerResumedAt);
        Assert.Equal(report.PublishedAt, report.PublishedTimerResumedAt);
        Assert.Equal(0, report.PublishedSecondsElapsed);
    }

    [Fact]
    public async Task Approve_claim_pauses_published_timer_and_freezes_elapsed_seconds()
    {
        await using var context = await ReportTestContext.CreateAsync(factory);
        var reportId = await ClaimTestHelpers.PublishLostReportAsync(context);

        const int elapsedBeforePause = 7_200;
        var resumedAt = DateTimeOffset.UtcNow.AddSeconds(-elapsedBeforePause);
        var report = await context.DbContext.Reports.SingleAsync(item => item.Id == reportId);
        report.PublishedTimerResumedAt = resumedAt;
        report.PublishedAt = resumedAt;
        await context.DbContext.SaveChangesAsync();

        var claimantSession = await ClaimTestHelpers.CreateAndLoginClaimantAsync(context);
        ClaimTestHelpers.Authenticate(context.Client, claimantSession.AccessToken);

        var (_, submitted) = await ClaimTestHelpers.SubmitClaimAsync(context.Client, reportId);
        Assert.NotNull(submitted);

        ClaimTestHelpers.AuthenticateReporter(context.Client, context);
        var approveResponse = await ClaimTestHelpers.ApproveClaimAsync(context.Client, submitted.Id);
        Assert.Equal(HttpStatusCode.NoContent, approveResponse.StatusCode);

        var pausedReport = await context.DbContext.Reports
            .AsNoTracking()
            .SingleAsync(item => item.Id == reportId);

        Assert.Equal(ReportStatus.ClaimInProgress, pausedReport.Status);
        Assert.Null(pausedReport.PublishedTimerResumedAt);
        Assert.InRange(
            pausedReport.PublishedSecondsElapsed,
            elapsedBeforePause - 5,
            elapsedBeforePause + 5);
    }

    [Fact]
    public async Task Cancel_claim_resumes_published_timer_with_remaining_time()
    {
        await using var context = await ReportTestContext.CreateAsync(factory);
        var scenario = await ResolutionTestHelpers.CreateApprovedClaimScenarioAsync(context);

        const int elapsedBeforePause = 5_400;
        var report = await context.DbContext.Reports.SingleAsync(item => item.Id == scenario.ReportId);
        report.PublishedSecondsElapsed = elapsedBeforePause;
        report.PublishedTimerResumedAt = null;
        await context.DbContext.SaveChangesAsync();

        ClaimTestHelpers.AuthenticateReporter(context.Client, context);
        var cancelResponse = await ResolutionTestHelpers.CancelClaimAsync(context.Client, scenario.ClaimId);
        Assert.Equal(HttpStatusCode.NoContent, cancelResponse.StatusCode);

        var resumedReport = await context.DbContext.Reports
            .AsNoTracking()
            .SingleAsync(item => item.Id == scenario.ReportId);

        Assert.Equal(ReportStatus.Published, resumedReport.Status);
        Assert.Equal(elapsedBeforePause, resumedReport.PublishedSecondsElapsed);
        Assert.NotNull(resumedReport.PublishedTimerResumedAt);
        Assert.InRange(
            (DateTimeOffset.UtcNow - resumedReport.PublishedTimerResumedAt.Value).TotalSeconds,
            0,
            10);
    }

    [Fact]
    public async Task Pending_review_report_does_not_initialize_published_timer()
    {
        await using var context = await ReportTestContext.CreateAsync(factory);
        var (_, created) = await context.SubmitReportAsync(TestReportHelpers.BuildValidLostRequest());
        Assert.NotNull(created);

        var report = await context.DbContext.Reports
            .AsNoTracking()
            .SingleAsync(item => item.Id == created.Id);

        Assert.Equal(ReportStatus.PendingReview, report.Status);
        Assert.Null(report.PublishedAt);
        Assert.Null(report.PublishedTimerResumedAt);
        Assert.Equal(0, report.PublishedSecondsElapsed);
    }

    [Fact]
    public async Task Reject_report_does_not_initialize_published_timer()
    {
        await using var context = await ReportTestContext.CreateAsync(factory);
        var (_, created) = await context.SubmitReportAsync(TestReportHelpers.BuildValidLostRequest());
        Assert.NotNull(created);

        await HttpTestHelpers.LoginAsAdminAsync(context);

        var rejectResponse = await context.Client.PostAsJsonAsync(
            $"/api/v1/admin/moderation/reports/{created.Id}/reject",
            new RejectReportRequest
            {
                ReasonCode = "rejection.insufficient_description",
            });
        Assert.Equal(HttpStatusCode.NoContent, rejectResponse.StatusCode);

        var report = await context.DbContext.Reports
            .AsNoTracking()
            .SingleAsync(item => item.Id == created.Id);

        Assert.Equal(ReportStatus.Rejected, report.Status);
        Assert.Null(report.PublishedAt);
        Assert.Null(report.PublishedTimerResumedAt);
        Assert.Equal(0, report.PublishedSecondsElapsed);
    }

    [Fact]
    public void GetCumulativePublishedSeconds_includes_running_segment_when_timer_is_active()
    {
        var lifecycleService = new ReportLifecycleService();
        var startedAt = new DateTimeOffset(2026, 9, 1, 12, 0, 0, TimeSpan.Zero);
        var now = startedAt.AddHours(2);

        var report = new Report
        {
            ReporterId = Guid.NewGuid(),
            Type = ReportType.Lost,
            CategoryId = Guid.NewGuid(),
            Title = "Timer test",
            Description = "Description",
            DateLostOrFound = DateOnly.FromDateTime(startedAt.UtcDateTime),
            GovernorateId = Guid.NewGuid(),
            Status = ReportStatus.Published,
            HiddenDetail = "Hidden",
            PublishedAt = startedAt,
            PublishedTimerResumedAt = startedAt,
            PublishedSecondsElapsed = 3_600,
        };

        var cumulativeSeconds = lifecycleService.GetCumulativePublishedSeconds(report, now);

        Assert.Equal(3_600 + 7_200, cumulativeSeconds);
    }

    [Fact]
    public async Task GetCumulativePublishedSeconds_uses_frozen_elapsed_seconds_when_timer_is_paused()
    {
        await using var serviceScope = factory.Services.CreateAsyncScope();
        var lifecycleService = serviceScope.ServiceProvider.GetRequiredService<IReportLifecycleService>();

        var report = new Report
        {
            ReporterId = Guid.NewGuid(),
            Type = ReportType.Lost,
            CategoryId = Guid.NewGuid(),
            Title = "Paused timer",
            Description = "Description",
            DateLostOrFound = DateOnly.FromDateTime(DateTime.UtcNow),
            GovernorateId = Guid.NewGuid(),
            Status = ReportStatus.ClaimInProgress,
            HiddenDetail = "Hidden",
            PublishedSecondsElapsed = 1_800,
            PublishedTimerResumedAt = null,
        };

        var cumulativeSeconds = lifecycleService.GetCumulativePublishedSeconds(
            report,
            DateTimeOffset.UtcNow.AddDays(30));

        Assert.Equal(1_800, cumulativeSeconds);
    }
}
