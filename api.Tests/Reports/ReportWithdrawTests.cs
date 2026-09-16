using System.Net;
using Amanah.Api.Data.Entities;
using Amanah.Api.Services.Lifecycle;
using Amanah.Api.Services.Storage;
using Amanah.Api.Tests.Claims;
using Amanah.Api.Tests.Infrastructure;
using Amanah.Api.Tests.Uploads;
using Amanah.Contracts.Errors;
using Amanah.Contracts.Requests.Reports;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Amanah.Api.Tests.Reports;

public class ReportWithdrawTests(ApiWebApplicationFactory factory) : IClassFixture<ApiWebApplicationFactory>
{
    [Fact]
    public async Task Withdraw_pending_report_sets_status_to_withdrawn()
    {
        await using var context = await ReportTestContext.CreateAsync(factory);
        var (_, created) = await context.SubmitReportAsync(TestReportHelpers.BuildValidLostRequest());
        Assert.NotNull(created);

        var response = await context.WithdrawReportAsync(
            created.Id,
            new() { Reason = "no_longer_needed" });

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        var report = await context.DbContext.Reports.SingleAsync(report => report.Id == created.Id);
        Assert.Equal(ReportStatus.Withdrawn, report.Status);
        Assert.Equal("no_longer_needed", report.WithdrawalReason);
    }

    [Fact]
    public async Task Withdraw_published_report_sets_status_to_withdrawn()
    {
        await using var context = await ReportTestContext.CreateAsync(factory);
        var reportId = await ClaimTestHelpers.PublishLostReportAsync(context);

        var response = await context.WithdrawReportAsync(
            reportId,
            new() { Reason = "recovered_outside" });

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        var report = await context.DbContext.Reports.SingleAsync(item => item.Id == reportId);
        Assert.Equal(ReportStatus.Withdrawn, report.Status);
        Assert.Equal("recovered_outside", report.WithdrawalReason);
    }

    [Fact]
    public async Task Withdraw_published_report_closes_pending_claims()
    {
        await using var context = await ReportTestContext.CreateAsync(factory);
        var reportId = await ClaimTestHelpers.PublishLostReportAsync(context);

        var claimant = await ClaimTestHelpers.CreateAndLoginClaimantAsync(context);
        var pendingClaimId = await ClaimTestHelpers.SeedPendingClaimAsync(
            context,
            reportId,
            claimant.User.Id);

        ClaimTestHelpers.AuthenticateReporter(context.Client, context);

        var response = await context.WithdrawReportAsync(
            reportId,
            new() { Reason = "no_longer_needed" });
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        var pendingClaim = await context.DbContext.Claims
            .AsNoTracking()
            .SingleAsync(claim => claim.Id == pendingClaimId);
        Assert.Equal(ClaimStatus.Withdrawn, pendingClaim.Status);
        Assert.Equal(ReportLifecycleService.ClosedReviewerDecision, pendingClaim.ReviewerDecision);
        Assert.Equal("no_longer_needed", pendingClaim.DecisionReason);
        Assert.False(pendingClaim.CountsAsFailure);
    }

    [Fact]
    public async Task Withdraw_published_report_with_approved_claim_returns_conflict()
    {
        await using var context = await ReportTestContext.CreateAsync(factory);
        var reportId = await ClaimTestHelpers.PublishLostReportAsync(context);

        var claimantSession = await ClaimTestHelpers.CreateAndLoginClaimantAsync(context);
        ClaimTestHelpers.Authenticate(context.Client, claimantSession.AccessToken);

        var (_, submitted) = await ClaimTestHelpers.SubmitClaimAsync(context.Client, reportId);
        Assert.NotNull(submitted);

        ClaimTestHelpers.AuthenticateReporter(context.Client, context);
        var approveResponse = await ClaimTestHelpers.ApproveClaimAsync(context.Client, submitted.Id);
        Assert.Equal(HttpStatusCode.NoContent, approveResponse.StatusCode);

        var response = await context.WithdrawReportAsync(reportId);
        var error = await HttpTestHelpers.ReadErrorAsync(response);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(ErrorCodes.Conflict, error?.Code);

        var report = await context.DbContext.Reports
            .AsNoTracking()
            .SingleAsync(item => item.Id == reportId);
        Assert.Equal(ReportStatus.ClaimInProgress, report.Status);
    }

    [Fact]
    public async Task Withdraw_non_owned_report_returns_not_found()
    {
        await using var ownerContext = await ReportTestContext.CreateAsync(factory);
        var (_, created) = await ownerContext.SubmitReportAsync(TestReportHelpers.BuildValidLostRequest());
        Assert.NotNull(created);

        await using var otherContext = await ReportTestContext.CreateAsync(factory);
        var response = await otherContext.WithdrawReportAsync(created.Id);
        var error = await HttpTestHelpers.ReadErrorAsync(response);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(ErrorCodes.NotFound, error?.Code);
    }

    [Fact]
    public async Task Withdraw_non_withdrawable_report_returns_conflict()
    {
        await using var context = await ReportTestContext.CreateAsync(factory);
        var (_, created) = await context.SubmitReportAsync(TestReportHelpers.BuildValidLostRequest());
        Assert.NotNull(created);

        var report = await context.DbContext.Reports.SingleAsync(report => report.Id == created.Id);
        report.Status = ReportStatus.Resolved;
        await context.DbContext.SaveChangesAsync();

        var response = await context.WithdrawReportAsync(created.Id);
        var error = await HttpTestHelpers.ReadErrorAsync(response);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(ErrorCodes.Conflict, error?.Code);
    }

    [Fact]
    public async Task Withdraw_deletes_report_photos_from_database_and_storage()
    {
        await using var context = await ReportTestContext.CreateAsync(factory);
        var (_, created) = await context.SubmitReportAsync(
            TestReportHelpers.BuildValidLostRequest(),
            [TestImageFactory.CreateMinimalJpeg()]);
        Assert.NotNull(created);

        await HttpTestHelpers.ApproveAsAdminAsync(context, created.Id);

        var photo = await context.DbContext.ReportPhotos
            .AsNoTracking()
            .SingleAsync(item => item.ReportId == created.Id);
        var storage = Assert.IsType<FakeBucketStorage>(
            factory.Services.GetRequiredService<IBucketStorage>());
        Assert.True(storage.ContainsKey(photo.StorageKey));
        Assert.True(storage.ContainsKey(photo.ThumbnailStorageKey!));

        var response = await context.WithdrawReportAsync(
            created.Id,
            new() { Reason = "no_longer_needed" });
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        Assert.Equal(0, await context.DbContext.ReportPhotos.CountAsync());
        Assert.False(storage.ContainsKey(photo.StorageKey));
        Assert.False(storage.ContainsKey(photo.ThumbnailStorageKey!));
    }
}
