using System.Net;
using System.Net.Http.Json;
using Amanah.Api.Tests.Chats;
using Amanah.Api.Tests.Claims;
using Amanah.Api.Tests.Infrastructure;
using Amanah.Api.Tests.Reports;
using Amanah.Api.Utilities.Abuse;
using Amanah.Contracts.Errors;
using Amanah.Contracts.Requests.Abuse;
using Amanah.Contracts.Requests.Chats;
using Amanah.Contracts.Responses.Browse;

namespace Amanah.Api.Tests.Admin;

public class AdminParticipationTests(ApiWebApplicationFactory factory) : IClassFixture<ApiWebApplicationFactory>
{
    [Fact]
    public async Task Admin_cannot_submit_report()
    {
        await using var context = await ReportTestContext.CreateAsync(factory);
        await HttpTestHelpers.LoginAsAdminAsync(context);

        var (response, _) = await context.SubmitReportAsync(TestReportHelpers.BuildValidLostRequest());
        var error = await HttpTestHelpers.ReadErrorAsync(response);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(ErrorCodes.AdminParticipationForbidden, error?.Code);
    }

    [Fact]
    public async Task Admin_cannot_flag_or_claim_published_listing()
    {
        await using var context = await ReportTestContext.CreateAsync(factory);
        var reportId = await ClaimTestHelpers.PublishLostReportAsync(context);

        await HttpTestHelpers.LoginAsAdminAsync(context);

        var flagResponse = await context.Client.PostAsJsonAsync(
            $"/api/v1/reports/{reportId}/flag",
            new FlagListingRequest { Reason = AbuseFlagReasons.Spam });
        var flagError = await HttpTestHelpers.ReadErrorAsync(flagResponse);

        Assert.Equal(HttpStatusCode.Forbidden, flagResponse.StatusCode);
        Assert.Equal(ErrorCodes.AdminParticipationForbidden, flagError?.Code);

        var (claimResponse, _) = await ClaimTestHelpers.SubmitClaimAsync(context.Client, reportId);
        var claimError = await HttpTestHelpers.ReadErrorAsync(claimResponse);

        Assert.Equal(HttpStatusCode.Forbidden, claimResponse.StatusCode);
        Assert.Equal(ErrorCodes.AdminParticipationForbidden, claimError?.Code);
    }

    [Fact]
    public async Task Admin_cannot_send_chat_message()
    {
        await using var context = await ReportTestContext.CreateAsync(factory);
        var reportId = await ClaimTestHelpers.PublishLostReportAsync(context);

        var claimantSession = await ClaimTestHelpers.CreateAndLoginClaimantAsync(context);
        ClaimTestHelpers.Authenticate(context.Client, claimantSession.AccessToken);
        var (_, claimBody) = await ClaimTestHelpers.SubmitClaimAsync(context.Client, reportId);
        Assert.NotNull(claimBody);

        ClaimTestHelpers.Authenticate(context.Client, context.Session.AccessToken);
        await ClaimTestHelpers.ApproveClaimAsync(context.Client, claimBody.Id);
        var threadId = await ChatTestHelpers.GetThreadIdAsync(context.Client, claimBody.Id);

        await HttpTestHelpers.LoginAsAdminAsync(context);

        var sendResponse = await context.Client.PostAsJsonAsync(
            $"/api/v1/chats/{threadId}/messages",
            new SendMessageRequest { Body = "Admin should not send this." });
        var sendError = await HttpTestHelpers.ReadErrorAsync(sendResponse);

        Assert.Equal(HttpStatusCode.Forbidden, sendResponse.StatusCode);
        Assert.Equal(ErrorCodes.AdminParticipationForbidden, sendError?.Code);
    }

    [Fact]
    public async Task Admin_can_browse_public_detail_and_use_moderation_queue()
    {
        await using var context = await ReportTestContext.CreateAsync(factory);
        var reportId = await ClaimTestHelpers.PublishLostReportAsync(context);

        await HttpTestHelpers.LoginAsAdminAsync(context);

        var detailResponse = await context.Client.GetAsync($"/api/v1/reports/{reportId}/public");
        var detail = await detailResponse.Content.ReadFromJsonAsync<PublicReportDetailResponse>();

        Assert.Equal(HttpStatusCode.OK, detailResponse.StatusCode);
        Assert.NotNull(detail);
        Assert.Equal(reportId, detail.Id);

        var queueResponse = await context.Client.GetAsync("/api/v1/admin/moderation/queue");
        Assert.Equal(HttpStatusCode.OK, queueResponse.StatusCode);
    }

    [Fact]
    public async Task Admin_can_read_investigation_chat_but_cannot_send()
    {
        await using var context = await ReportTestContext.CreateAsync(factory);
        var reportId = await ClaimTestHelpers.PublishLostReportAsync(context);

        var claimantSession = await ClaimTestHelpers.CreateAndLoginClaimantAsync(context);
        ClaimTestHelpers.Authenticate(context.Client, claimantSession.AccessToken);
        var (_, claimBody) = await ClaimTestHelpers.SubmitClaimAsync(context.Client, reportId);
        Assert.NotNull(claimBody);

        ClaimTestHelpers.Authenticate(context.Client, context.Session.AccessToken);
        await ClaimTestHelpers.ApproveClaimAsync(context.Client, claimBody.Id);
        var threadId = await ChatTestHelpers.GetThreadIdAsync(context.Client, claimBody.Id);

        ClaimTestHelpers.Authenticate(context.Client, claimantSession.AccessToken);
        await ChatTestHelpers.SendMessageAsync(context.Client, threadId, "User message");

        var flaggerSession = await ClaimTestHelpers.CreateAndLoginClaimantAsync(context);
        ClaimTestHelpers.Authenticate(context.Client, flaggerSession.AccessToken);
        var flagResponse = await context.Client.PostAsJsonAsync(
            $"/api/v1/reports/{reportId}/flag",
            new FlagListingRequest { Reason = AbuseFlagReasons.Other });
        Assert.Equal(HttpStatusCode.OK, flagResponse.StatusCode);

        await HttpTestHelpers.LoginAsAdminAsync(context);

        var investigationResponse = await context.Client.GetAsync(
            $"/api/v1/admin/investigations/{reportId}/chat");
        Assert.Equal(HttpStatusCode.OK, investigationResponse.StatusCode);

        var threadResponse = await context.Client.GetAsync($"/api/v1/chats/{threadId}");
        Assert.Equal(HttpStatusCode.OK, threadResponse.StatusCode);

        var sendResponse = await context.Client.PostAsJsonAsync(
            $"/api/v1/chats/{threadId}/messages",
            new SendMessageRequest { Body = "Admin reply." });
        var sendError = await HttpTestHelpers.ReadErrorAsync(sendResponse);

        Assert.Equal(HttpStatusCode.Forbidden, sendResponse.StatusCode);
        Assert.Equal(ErrorCodes.AdminParticipationForbidden, sendError?.Code);
    }
}
