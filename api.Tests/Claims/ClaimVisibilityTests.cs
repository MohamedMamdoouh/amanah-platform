using System.Net;
using Amanah.Api.Tests.Infrastructure;
using Amanah.Api.Tests.Reports;
using Amanah.Contracts.Errors;
using Microsoft.EntityFrameworkCore;

namespace Amanah.Api.Tests.Claims;

public class ClaimVisibilityTests(ApiWebApplicationFactory factory) : IClassFixture<ApiWebApplicationFactory>
{
    [Fact]
    public async Task Claimant_can_read_own_claim_detail()
    {
        await using var context = await ReportTestContext.CreateAsync(factory);
        var reportId = await ClaimTestHelpers.PublishLostReportAsync(context);
        var claimantSession = await ClaimTestHelpers.CreateAndLoginClaimantAsync(context);
        ClaimTestHelpers.Authenticate(context.Client, claimantSession.AccessToken);

        var (_, submitted) = await ClaimTestHelpers.SubmitClaimAsync(context.Client, reportId);
        Assert.NotNull(submitted);

        var (response, body) = await ClaimTestHelpers.GetClaimAsync(context.Client, submitted.Id);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body);
        Assert.Equal(submitted.Id, body.Id);
        Assert.Equal("pending", body.Status);
        Assert.Equal(ClaimTestHelpers.ValidAnswer, body.SubmittedAnswer);
        Assert.Equal(reportId, body.ReportId);
        Assert.Equal("Claimant", body.ClaimantDisplayName);
        Assert.Equal(context.Session.User.DisplayName, body.ReporterDisplayName);
    }

    [Fact]
    public async Task Reporter_can_read_claim_detail_on_own_report()
    {
        await using var context = await ReportTestContext.CreateAsync(factory);
        var reportId = await ClaimTestHelpers.PublishLostReportAsync(context);
        var claimantSession = await ClaimTestHelpers.CreateAndLoginClaimantAsync(context);
        ClaimTestHelpers.Authenticate(context.Client, claimantSession.AccessToken);

        var (_, submitted) = await ClaimTestHelpers.SubmitClaimAsync(context.Client, reportId);
        Assert.NotNull(submitted);

        ClaimTestHelpers.AuthenticateReporter(context.Client, context);
        var (response, body) = await ClaimTestHelpers.GetClaimAsync(context.Client, submitted.Id);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body);
        Assert.Equal(submitted.Id, body.Id);
        Assert.Equal("Claimant", body.ClaimantDisplayName);
        Assert.Equal(context.Session.User.DisplayName, body.ReporterDisplayName);
    }

    [Fact]
    public async Task Third_party_cannot_read_claim_detail()
    {
        await using var context = await ReportTestContext.CreateAsync(factory);
        var reportId = await ClaimTestHelpers.PublishLostReportAsync(context);
        var claimantSession = await ClaimTestHelpers.CreateAndLoginClaimantAsync(context);
        ClaimTestHelpers.Authenticate(context.Client, claimantSession.AccessToken);

        var (_, submitted) = await ClaimTestHelpers.SubmitClaimAsync(context.Client, reportId);
        Assert.NotNull(submitted);

        var strangerSession = await ClaimTestHelpers.CreateAndLoginClaimantAsync(context);
        ClaimTestHelpers.Authenticate(context.Client, strangerSession.AccessToken);

        var (response, errorBody) = await ClaimTestHelpers.GetClaimAsync(context.Client, submitted.Id);
        var error = await HttpTestHelpers.ReadErrorAsync(response);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(ErrorCodes.NotFound, error?.Code);
        Assert.Null(errorBody);
    }

    [Fact]
    public async Task Get_mine_returns_only_authenticated_claimant_claims()
    {
        await using var context = await ReportTestContext.CreateAsync(factory);
        var firstReportId = await ClaimTestHelpers.PublishLostReportAsync(context);
        var secondReportId = await ClaimTestHelpers.PublishFoundReportAsync(context);

        var firstClaimant = await ClaimTestHelpers.CreateAndLoginClaimantAsync(context);
        ClaimTestHelpers.Authenticate(context.Client, firstClaimant.AccessToken);
        var (_, firstClaim) = await ClaimTestHelpers.SubmitClaimAsync(context.Client, firstReportId);
        Assert.NotNull(firstClaim);

        var secondClaimant = await ClaimTestHelpers.CreateAndLoginClaimantAsync(context);
        ClaimTestHelpers.Authenticate(context.Client, secondClaimant.AccessToken);
        var (_, secondClaim) = await ClaimTestHelpers.SubmitClaimAsync(
            context.Client,
            secondReportId,
            "Lost blue backpack with school books and a name tag inside.");
        Assert.NotNull(secondClaim);

        ClaimTestHelpers.Authenticate(context.Client, firstClaimant.AccessToken);
        var (response, body) = await ClaimTestHelpers.GetMyClaimsAsync(context.Client);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body);
        Assert.Equal(1, body.TotalCount);
        Assert.Single(body.Items);
        Assert.Equal(firstClaim.Id, body.Items[0].Id);
        Assert.Equal(context.Session.User.DisplayName, body.Items[0].ReporterDisplayName);
    }

    [Fact]
    public async Task Get_mine_rejects_page_size_above_max()
    {
        await using var context = await ReportTestContext.CreateAsync(factory);
        var reportId = await ClaimTestHelpers.PublishLostReportAsync(context);
        var claimantSession = await ClaimTestHelpers.CreateAndLoginClaimantAsync(context);
        ClaimTestHelpers.Authenticate(context.Client, claimantSession.AccessToken);
        await ClaimTestHelpers.SubmitClaimAsync(context.Client, reportId);

        var (tooLargeResponse, _) = await ClaimTestHelpers.GetMyClaimsAsync(
            context.Client,
            page: 1,
            pageSize: 100);
        var tooLargeError = await HttpTestHelpers.ReadErrorAsync(tooLargeResponse);

        Assert.Equal(HttpStatusCode.BadRequest, tooLargeResponse.StatusCode);
        Assert.Equal(ErrorCodes.ValidationFailed, tooLargeError?.Code);
        Assert.True(tooLargeError?.Errors?.ContainsKey("pageSize") ?? false);

        var (maxResponse, maxBody) = await ClaimTestHelpers.GetMyClaimsAsync(
            context.Client,
            page: 1,
            pageSize: 50);

        Assert.Equal(HttpStatusCode.OK, maxResponse.StatusCode);
        Assert.NotNull(maxBody);
        Assert.Equal(1, maxBody.TotalCount);
        Assert.Equal(50, maxBody.PageSize);
    }

    [Fact]
    public async Task Approved_claim_detail_includes_chat_thread_id_for_claimant()
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

        var chatThreadId = await context.DbContext.ChatThreads
            .AsNoTracking()
            .Where(thread => thread.ClaimId == submitted.Id)
            .Select(thread => thread.Id)
            .SingleAsync();

        ClaimTestHelpers.Authenticate(context.Client, claimantSession.AccessToken);
        var (response, body) = await ClaimTestHelpers.GetClaimAsync(context.Client, submitted.Id);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body);
        Assert.Equal("approved", body.Status);
        Assert.Equal("claim_in_progress", body.ReportStatus);
        Assert.Equal(chatThreadId, body.ChatThreadId);
    }

    [Fact]
    public async Task Reporter_can_list_claims_on_own_report()
    {
        await using var context = await ReportTestContext.CreateAsync(factory);
        var reportId = await ClaimTestHelpers.PublishLostReportAsync(context);
        var claimantSession = await ClaimTestHelpers.CreateAndLoginClaimantAsync(context);
        ClaimTestHelpers.Authenticate(context.Client, claimantSession.AccessToken);

        var (_, submitted) = await ClaimTestHelpers.SubmitClaimAsync(context.Client, reportId);
        Assert.NotNull(submitted);

        ClaimTestHelpers.AuthenticateReporter(context.Client, context);
        var (response, body) = await ClaimTestHelpers.GetReportClaimsAsync(context.Client, reportId);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body);
        Assert.Single(body);
        Assert.Equal(submitted.Id, body[0].Id);
        Assert.Equal("pending", body[0].Status);
        Assert.Equal(ClaimTestHelpers.ValidAnswer, body[0].SubmittedAnswer);
        Assert.Equal("Claimant", body[0].ClaimantDisplayName);
    }

    [Fact]
    public async Task Stranger_cannot_list_claims_on_report()
    {
        await using var context = await ReportTestContext.CreateAsync(factory);
        var reportId = await ClaimTestHelpers.PublishLostReportAsync(context);
        var claimantSession = await ClaimTestHelpers.CreateAndLoginClaimantAsync(context);
        ClaimTestHelpers.Authenticate(context.Client, claimantSession.AccessToken);
        await ClaimTestHelpers.SubmitClaimAsync(context.Client, reportId);

        var strangerSession = await ClaimTestHelpers.CreateAndLoginClaimantAsync(context);
        ClaimTestHelpers.Authenticate(context.Client, strangerSession.AccessToken);

        var (response, error) = await ClaimTestHelpers.GetReportClaimsAsync(context.Client, reportId);
        var apiError = await HttpTestHelpers.ReadErrorAsync(response);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(ErrorCodes.NotFound, apiError?.Code);
        Assert.Null(error);
    }
}
