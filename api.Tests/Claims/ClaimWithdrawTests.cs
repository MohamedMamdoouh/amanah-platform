using System.Net;
using Amanah.Api.Data.Entities;
using Amanah.Api.Tests.Infrastructure;
using Amanah.Api.Tests.Reports;
using Amanah.Contracts.Errors;
using Microsoft.EntityFrameworkCore;

namespace Amanah.Api.Tests.Claims;

public class ClaimWithdrawTests(ApiWebApplicationFactory factory) : IClassFixture<ApiWebApplicationFactory>
{
    [Fact]
    public async Task Withdraw_pending_claim_sets_status_without_consuming_attempt()
    {
        await using var context = await ReportTestContext.CreateAsync(factory);
        var reportId = await ClaimTestHelpers.PublishLostReportAsync(context);
        var claimantSession = await ClaimTestHelpers.CreateAndLoginClaimantAsync(context);
        ClaimTestHelpers.Authenticate(context.Client, claimantSession.AccessToken);

        var (_, submitted) = await ClaimTestHelpers.SubmitClaimAsync(context.Client, reportId);
        Assert.NotNull(submitted);

        var response = await ClaimTestHelpers.WithdrawClaimAsync(context.Client, submitted.Id);
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        var claim = await context.DbContext.Claims
            .AsNoTracking()
            .SingleAsync(existingClaim => existingClaim.Id == submitted.Id);
        Assert.Equal(ClaimStatus.Withdrawn, claim.Status);
        Assert.False(claim.CountsAsFailure);
        Assert.NotNull(claim.ReviewedAt);

        var report = await context.DbContext.Reports
            .AsNoTracking()
            .SingleAsync(existingReport => existingReport.Id == reportId);
        Assert.Equal(ReportStatus.Published, report.Status);
    }

    [Fact]
    public async Task Withdraw_notifies_reporter()
    {
        await using var context = await ReportTestContext.CreateAsync(factory);
        var reportId = await ClaimTestHelpers.PublishLostReportAsync(context);
        var claimantSession = await ClaimTestHelpers.CreateAndLoginClaimantAsync(context);
        ClaimTestHelpers.Authenticate(context.Client, claimantSession.AccessToken);

        var (_, submitted) = await ClaimTestHelpers.SubmitClaimAsync(context.Client, reportId);
        Assert.NotNull(submitted);

        var response = await ClaimTestHelpers.WithdrawClaimAsync(context.Client, submitted.Id);
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        var notification = await context.DbContext.Notifications
            .AsNoTracking()
            .SingleAsync(item =>
                item.UserId == context.Session.User.Id
                && item.Type == "ClaimWithdrawnByClaimant");
        Assert.Equal("ClaimWithdrawnByClaimant", notification.Type);
        Assert.Contains($"/my/reports/{reportId}", notification.PayloadJson, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Withdraw_allows_new_claim_without_counting_attempt()
    {
        await using var context = await ReportTestContext.CreateAsync(factory);
        var reportId = await ClaimTestHelpers.PublishLostReportAsync(context);
        var claimantSession = await ClaimTestHelpers.CreateAndLoginClaimantAsync(context);
        ClaimTestHelpers.Authenticate(context.Client, claimantSession.AccessToken);

        var (_, submitted) = await ClaimTestHelpers.SubmitClaimAsync(context.Client, reportId);
        Assert.NotNull(submitted);

        var withdrawResponse = await ClaimTestHelpers.WithdrawClaimAsync(context.Client, submitted.Id);
        Assert.Equal(HttpStatusCode.NoContent, withdrawResponse.StatusCode);

        var (_, resubmitted) = await ClaimTestHelpers.SubmitClaimAsync(
            context.Client,
            reportId,
            "Blue fabric wallet with a school ID card inside.");
        Assert.NotNull(resubmitted);
        Assert.Equal("pending", resubmitted.Status);

        var claims = await context.DbContext.Claims
            .AsNoTracking()
            .Where(existingClaim =>
                existingClaim.ReportId == reportId
                && existingClaim.ClaimantId == claimantSession.User.Id)
            .ToListAsync();
        Assert.Equal(2, claims.Count);
        Assert.All(claims, existingClaim => Assert.False(existingClaim.CountsAsFailure));
        Assert.Equal(2, claims.Max(existingClaim => existingClaim.AttemptNumber));
    }

    [Fact]
    public async Task Withdraw_after_approve_returns_conflict()
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

        ClaimTestHelpers.Authenticate(context.Client, claimantSession.AccessToken);
        var withdrawResponse = await ClaimTestHelpers.WithdrawClaimAsync(context.Client, submitted.Id);
        var error = await HttpTestHelpers.ReadErrorAsync(withdrawResponse);

        Assert.Equal(HttpStatusCode.Conflict, withdrawResponse.StatusCode);
        Assert.Equal(ErrorCodes.Conflict, error?.Code);
    }

    [Fact]
    public async Task Non_claimant_cannot_withdraw_claim()
    {
        await using var context = await ReportTestContext.CreateAsync(factory);
        var reportId = await ClaimTestHelpers.PublishLostReportAsync(context);
        var claimantSession = await ClaimTestHelpers.CreateAndLoginClaimantAsync(context);
        ClaimTestHelpers.Authenticate(context.Client, claimantSession.AccessToken);

        var (_, submitted) = await ClaimTestHelpers.SubmitClaimAsync(context.Client, reportId);
        Assert.NotNull(submitted);

        ClaimTestHelpers.AuthenticateReporter(context.Client, context);
        var response = await ClaimTestHelpers.WithdrawClaimAsync(context.Client, submitted.Id);
        var error = await HttpTestHelpers.ReadErrorAsync(response);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(ErrorCodes.NotFound, error?.Code);
    }
}
