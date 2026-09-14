using System.Net;
using Amanah.Api.Data.Entities;
using Amanah.Api.Services.Claims;
using Amanah.Api.Tests.Infrastructure;
using Amanah.Api.Tests.Reports;
using Amanah.Contracts.Errors;
using Microsoft.EntityFrameworkCore;

namespace Amanah.Api.Tests.Claims;

public class ClaimReviewTests(ApiWebApplicationFactory factory) : IClassFixture<ApiWebApplicationFactory>
{
    [Fact]
    public async Task Approve_moves_report_to_claim_in_progress_and_creates_chat_thread()
    {
        await using var context = await ReportTestContext.CreateAsync(factory);
        var reportId = await ClaimTestHelpers.PublishLostReportAsync(context);
        var claimantSession = await ClaimTestHelpers.CreateAndLoginClaimantAsync(context);
        ClaimTestHelpers.Authenticate(context.Client, claimantSession.AccessToken);

        var (_, submitted) = await ClaimTestHelpers.SubmitClaimAsync(context.Client, reportId);
        Assert.NotNull(submitted);

        ClaimTestHelpers.AuthenticateReporter(context.Client, context);

        var response = await ClaimTestHelpers.ApproveClaimAsync(context.Client, submitted.Id);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        var claim = await context.DbContext.Claims
            .AsNoTracking()
            .SingleAsync(existingClaim => existingClaim.Id == submitted.Id);
        Assert.Equal(ClaimStatus.Approved, claim.Status);
        Assert.Equal("approved", claim.ReviewerDecision);
        Assert.NotNull(claim.ReviewedAt);

        var report = await context.DbContext.Reports
            .AsNoTracking()
            .SingleAsync(existingReport => existingReport.Id == reportId);
        Assert.Equal(ReportStatus.ClaimInProgress, report.Status);

        var chatThread = await context.DbContext.ChatThreads
            .AsNoTracking()
            .SingleAsync(thread => thread.ClaimId == submitted.Id);
        Assert.NotEqual(default, chatThread.CreatedAt);
    }

    [Fact]
    public async Task Approve_notifies_claimant_with_chat_deep_link()
    {
        await using var context = await ReportTestContext.CreateAsync(factory);
        var reportId = await ClaimTestHelpers.PublishLostReportAsync(context);
        var claimantSession = await ClaimTestHelpers.CreateAndLoginClaimantAsync(context);
        ClaimTestHelpers.Authenticate(context.Client, claimantSession.AccessToken);

        var (_, submitted) = await ClaimTestHelpers.SubmitClaimAsync(context.Client, reportId);
        Assert.NotNull(submitted);

        ClaimTestHelpers.AuthenticateReporter(context.Client, context);
        var response = await ClaimTestHelpers.ApproveClaimAsync(context.Client, submitted.Id);
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        var notification = await context.DbContext.Notifications
            .AsNoTracking()
            .SingleAsync(item => item.UserId == claimantSession.User.Id);
        Assert.Equal("ClaimApproved", notification.Type);
        Assert.Contains("/my/chats/", notification.PayloadJson, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Approve_auto_rejects_other_pending_claims_without_consuming_attempts()
    {
        await using var context = await ReportTestContext.CreateAsync(factory);
        var reportId = await ClaimTestHelpers.PublishLostReportAsync(context);

        var firstClaimant = await ClaimTestHelpers.CreateAndLoginClaimantAsync(context);
        ClaimTestHelpers.Authenticate(context.Client, firstClaimant.AccessToken);
        var (_, firstClaim) = await ClaimTestHelpers.SubmitClaimAsync(context.Client, reportId);
        Assert.NotNull(firstClaim);

        var secondClaimant = await ClaimTestHelpers.CreateAndLoginClaimantAsync(context);
        ClaimTestHelpers.Authenticate(context.Client, secondClaimant.AccessToken);
        var (_, secondClaim) = await ClaimTestHelpers.SubmitClaimAsync(
            context.Client,
            reportId,
            "Blue fabric wallet with a school ID card inside.");
        Assert.NotNull(secondClaim);

        ClaimTestHelpers.AuthenticateReporter(context.Client, context);
        var response = await ClaimTestHelpers.ApproveClaimAsync(context.Client, firstClaim.Id);
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        var rejectedClaim = await context.DbContext.Claims
            .AsNoTracking()
            .SingleAsync(existingClaim => existingClaim.Id == secondClaim.Id);
        Assert.Equal(ClaimStatus.Rejected, rejectedClaim.Status);
        Assert.Equal(ClaimService.AutoRejectReason, rejectedClaim.DecisionReason);
        Assert.False(rejectedClaim.CountsAsFailure);
        Assert.Equal("rejected", rejectedClaim.ReviewerDecision);
    }

    [Fact]
    public async Task Approve_notifies_auto_rejected_claimants()
    {
        await using var context = await ReportTestContext.CreateAsync(factory);
        var reportId = await ClaimTestHelpers.PublishLostReportAsync(context);

        var firstClaimant = await ClaimTestHelpers.CreateAndLoginClaimantAsync(context);
        ClaimTestHelpers.Authenticate(context.Client, firstClaimant.AccessToken);
        var (_, firstClaim) = await ClaimTestHelpers.SubmitClaimAsync(context.Client, reportId);
        Assert.NotNull(firstClaim);

        var secondClaimant = await ClaimTestHelpers.CreateAndLoginClaimantAsync(context);
        ClaimTestHelpers.Authenticate(context.Client, secondClaimant.AccessToken);
        var (_, secondClaim) = await ClaimTestHelpers.SubmitClaimAsync(
            context.Client,
            reportId,
            "Blue fabric wallet with a school ID card inside.");
        Assert.NotNull(secondClaim);

        ClaimTestHelpers.AuthenticateReporter(context.Client, context);
        var response = await ClaimTestHelpers.ApproveClaimAsync(context.Client, firstClaim.Id);
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        var notification = await context.DbContext.Notifications
            .AsNoTracking()
            .SingleAsync(item => item.UserId == secondClaimant.User.Id);
        Assert.Equal("ClaimRejected", notification.Type);
        Assert.Contains(ClaimService.AutoRejectReason, notification.PayloadJson, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Reject_marks_claim_as_failure_and_notifies_claimant()
    {
        await using var context = await ReportTestContext.CreateAsync(factory);
        var reportId = await ClaimTestHelpers.PublishLostReportAsync(context);
        var claimantSession = await ClaimTestHelpers.CreateAndLoginClaimantAsync(context);
        ClaimTestHelpers.Authenticate(context.Client, claimantSession.AccessToken);

        var (_, submitted) = await ClaimTestHelpers.SubmitClaimAsync(context.Client, reportId);
        Assert.NotNull(submitted);

        ClaimTestHelpers.AuthenticateReporter(context.Client, context);
        var response = await ClaimTestHelpers.RejectClaimAsync(context.Client, submitted.Id);
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        var claim = await context.DbContext.Claims
            .AsNoTracking()
            .SingleAsync(existingClaim => existingClaim.Id == submitted.Id);
        Assert.Equal(ClaimStatus.Rejected, claim.Status);
        Assert.True(claim.CountsAsFailure);
        Assert.Equal("rejected", claim.ReviewerDecision);
        Assert.NotNull(claim.ReviewedAt);

        var report = await context.DbContext.Reports
            .AsNoTracking()
            .SingleAsync(existingReport => existingReport.Id == reportId);
        Assert.Equal(ReportStatus.Published, report.Status);

        var notification = await context.DbContext.Notifications
            .AsNoTracking()
            .SingleAsync(item => item.UserId == claimantSession.User.Id);
        Assert.Equal("ClaimRejected", notification.Type);
        Assert.Contains($"/lost/{reportId}", notification.PayloadJson, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Approve_non_pending_claim_returns_conflict()
    {
        await using var context = await ReportTestContext.CreateAsync(factory);
        var reportId = await ClaimTestHelpers.PublishLostReportAsync(context);
        var claimantSession = await ClaimTestHelpers.CreateAndLoginClaimantAsync(context);
        ClaimTestHelpers.Authenticate(context.Client, claimantSession.AccessToken);

        var (_, submitted) = await ClaimTestHelpers.SubmitClaimAsync(context.Client, reportId);
        Assert.NotNull(submitted);

        ClaimTestHelpers.AuthenticateReporter(context.Client, context);
        var firstApprove = await ClaimTestHelpers.ApproveClaimAsync(context.Client, submitted.Id);
        Assert.Equal(HttpStatusCode.NoContent, firstApprove.StatusCode);

        var secondApprove = await ClaimTestHelpers.ApproveClaimAsync(context.Client, submitted.Id);
        var error = await HttpTestHelpers.ReadErrorAsync(secondApprove);

        Assert.Equal(HttpStatusCode.Conflict, secondApprove.StatusCode);
        Assert.Equal(ErrorCodes.Conflict, error?.Code);
    }

    [Fact]
    public async Task Reject_non_pending_claim_returns_conflict()
    {
        await using var context = await ReportTestContext.CreateAsync(factory);
        var reportId = await ClaimTestHelpers.PublishLostReportAsync(context);
        var claimantSession = await ClaimTestHelpers.CreateAndLoginClaimantAsync(context);
        ClaimTestHelpers.Authenticate(context.Client, claimantSession.AccessToken);

        var (_, submitted) = await ClaimTestHelpers.SubmitClaimAsync(context.Client, reportId);
        Assert.NotNull(submitted);

        ClaimTestHelpers.AuthenticateReporter(context.Client, context);
        var firstReject = await ClaimTestHelpers.RejectClaimAsync(context.Client, submitted.Id);
        Assert.Equal(HttpStatusCode.NoContent, firstReject.StatusCode);

        var secondReject = await ClaimTestHelpers.RejectClaimAsync(context.Client, submitted.Id);
        var error = await HttpTestHelpers.ReadErrorAsync(secondReject);

        Assert.Equal(HttpStatusCode.Conflict, secondReject.StatusCode);
        Assert.Equal(ErrorCodes.Conflict, error?.Code);
    }

    [Fact]
    public async Task Non_reporter_cannot_approve_claim()
    {
        await using var context = await ReportTestContext.CreateAsync(factory);
        var reportId = await ClaimTestHelpers.PublishLostReportAsync(context);
        var claimantSession = await ClaimTestHelpers.CreateAndLoginClaimantAsync(context);
        ClaimTestHelpers.Authenticate(context.Client, claimantSession.AccessToken);

        var (_, submitted) = await ClaimTestHelpers.SubmitClaimAsync(context.Client, reportId);
        Assert.NotNull(submitted);

        var response = await ClaimTestHelpers.ApproveClaimAsync(context.Client, submitted.Id);
        var error = await HttpTestHelpers.ReadErrorAsync(response);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(ErrorCodes.NotFound, error?.Code);
    }

    [Fact]
    public async Task Non_reporter_cannot_reject_claim()
    {
        await using var context = await ReportTestContext.CreateAsync(factory);
        var reportId = await ClaimTestHelpers.PublishLostReportAsync(context);
        var claimantSession = await ClaimTestHelpers.CreateAndLoginClaimantAsync(context);
        ClaimTestHelpers.Authenticate(context.Client, claimantSession.AccessToken);

        var (_, submitted) = await ClaimTestHelpers.SubmitClaimAsync(context.Client, reportId);
        Assert.NotNull(submitted);

        var response = await ClaimTestHelpers.RejectClaimAsync(context.Client, submitted.Id);
        var error = await HttpTestHelpers.ReadErrorAsync(response);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(ErrorCodes.NotFound, error?.Code);
    }
}
