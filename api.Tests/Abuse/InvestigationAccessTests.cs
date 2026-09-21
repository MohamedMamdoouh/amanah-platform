using System.Net;
using System.Net.Http.Json;
using Amanah.Api.Tests.Chats;
using Amanah.Api.Tests.Claims;
using Amanah.Api.Tests.Infrastructure;
using Amanah.Api.Tests.Reports;
using Amanah.Api.Tests.Uploads;
using Amanah.Api.Utilities.Abuse;
using Amanah.Contracts.Errors;
using Amanah.Contracts.Requests.Abuse;
using Amanah.Contracts.Requests.Admin;
using Amanah.Contracts.Requests.Claims;
using Amanah.Contracts.Responses.Uploads;
using Amanah.Contracts.Responses.Abuse;
using Amanah.Contracts.Responses.Admin;
using Amanah.Contracts.Responses.Chats;

namespace Amanah.Api.Tests.Abuse;

public class InvestigationAccessTests(ApiWebApplicationFactory factory) : IClassFixture<ApiWebApplicationFactory>
{
    [Fact]
    public async Task Admin_cannot_access_investigation_endpoints_without_open_flag()
    {
        await using var context = await ReportTestContext.CreateAsync(factory);
        var reportId = await ClaimTestHelpers.PublishLostReportAsync(context);

        await HttpTestHelpers.LoginAsAdminAsync(context);

        var chatResponse = await context.Client.GetAsync(
            $"/api/v1/admin/investigations/{reportId}/chat");
        var chatError = await HttpTestHelpers.ReadErrorAsync(chatResponse);

        Assert.Equal(HttpStatusCode.Forbidden, chatResponse.StatusCode);
        Assert.Equal(ErrorCodes.AbuseInvestigationUnavailable, chatError?.Code);
    }

    [Fact]
    public async Task Admin_can_access_investigation_chat_and_thread_after_flag()
    {
        await using var context = await ReportTestContext.CreateAsync(factory);
        var reportId = await ClaimTestHelpers.PublishLostReportAsync(context);

        var claimantSession = await ClaimTestHelpers.CreateAndLoginClaimantAsync(context);
        ClaimTestHelpers.Authenticate(context.Client, claimantSession.AccessToken);

        var (_, submitBody) = await ClaimTestHelpers.SubmitClaimAsync(
            context.Client,
            reportId,
            new SubmitClaimRequest
            {
                SubmittedAnswer = ClaimTestHelpers.ValidAnswer,
            });
        Assert.NotNull(submitBody);

        ClaimTestHelpers.Authenticate(context.Client, context.Session.AccessToken);
        await ClaimTestHelpers.ApproveClaimAsync(context.Client, submitBody.Id);

        var threadId = await ChatTestHelpers.GetThreadIdAsync(context.Client, submitBody.Id);
        ClaimTestHelpers.Authenticate(context.Client, claimantSession.AccessToken);
        await ChatTestHelpers.SendMessageAsync(context.Client, threadId, "Investigation probe message");

        var flaggerSession = await ClaimTestHelpers.CreateAndLoginClaimantAsync(context);
        ClaimTestHelpers.Authenticate(context.Client, flaggerSession.AccessToken);
        var (_, flagBody) = await FlagListingAsync(context.Client, reportId, AbuseFlagReasons.Other);
        Assert.NotNull(flagBody);

        await HttpTestHelpers.LoginAsAdminAsync(context);

        var investigationChatResponse = await context.Client.GetAsync(
            $"/api/v1/admin/investigations/{reportId}/chat");
        var investigationChat = await investigationChatResponse.Content
            .ReadFromJsonAsync<InvestigationChatResponse>();

        Assert.Equal(HttpStatusCode.OK, investigationChatResponse.StatusCode);
        Assert.NotNull(investigationChat);
        Assert.Single(investigationChat.Threads);
        Assert.Contains(
            "Investigation probe message",
            investigationChat.Threads[0].Messages.Select(message => message.Body));

        var directThreadResponse = await ChatTestHelpers.GetThreadAsync(context.Client, threadId);
        var directThread = await directThreadResponse.Content.ReadFromJsonAsync<ChatThreadDetailResponse>();

        Assert.Equal(HttpStatusCode.OK, directThreadResponse.StatusCode);
        Assert.NotNull(directThread);
        Assert.Contains("Investigation probe message", directThread.Messages.Select(message => message.Body));
    }

    [Fact]
    public async Task Admin_claim_photo_presign_requires_open_investigation()
    {
        await using var context = await ReportTestContext.CreateAsync(factory);
        var reportId = await ClaimTestHelpers.PublishLostReportAsync(context);

        var claimantSession = await ClaimTestHelpers.CreateAndLoginClaimantAsync(context);
        ClaimTestHelpers.Authenticate(context.Client, claimantSession.AccessToken);

        var (_, submitBody) = await ClaimTestHelpers.SubmitClaimAsync(
            context.Client,
            reportId,
            new SubmitClaimRequest
            {
                SubmittedAnswer = ClaimTestHelpers.ValidAnswer,
            },
            [TestImageFactory.CreateMinimalJpeg()]);
        Assert.NotNull(submitBody);

        await HttpTestHelpers.LoginAsAdminAsync(context);

        var blockedResponse = await context.Client.GetAsync(
            $"/api/v1/uploads/claim-photo/{submitBody.Id}/url");
        var blockedError = await HttpTestHelpers.ReadErrorAsync(blockedResponse);

        Assert.Equal(HttpStatusCode.Forbidden, blockedResponse.StatusCode);
        Assert.Equal(ErrorCodes.AbuseInvestigationUnavailable, blockedError?.Code);

        var flaggerSession = await ClaimTestHelpers.CreateAndLoginClaimantAsync(context);
        ClaimTestHelpers.Authenticate(context.Client, flaggerSession.AccessToken);
        await FlagListingAsync(context.Client, reportId, AbuseFlagReasons.Spam);

        await HttpTestHelpers.LoginAsAdminAsync(context);

        var allowedResponse = await context.Client.GetAsync(
            $"/api/v1/uploads/claim-photo/{submitBody.Id}/url");
        var allowedBody = await allowedResponse.Content.ReadFromJsonAsync<ClaimPhotoPresignResponse>();

        Assert.Equal(HttpStatusCode.OK, allowedResponse.StatusCode);
        Assert.NotNull(allowedBody);
        Assert.StartsWith("https://fake.local/", allowedBody.Url, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Admin_claim_photo_presign_blocked_after_abuse_resolve()
    {
        await using var context = await ReportTestContext.CreateAsync(factory);
        var reportId = await ClaimTestHelpers.PublishLostReportAsync(context);

        var claimantSession = await ClaimTestHelpers.CreateAndLoginClaimantAsync(context);
        ClaimTestHelpers.Authenticate(context.Client, claimantSession.AccessToken);

        var (_, submitBody) = await ClaimTestHelpers.SubmitClaimAsync(
            context.Client,
            reportId,
            new SubmitClaimRequest
            {
                SubmittedAnswer = ClaimTestHelpers.ValidAnswer,
            },
            [TestImageFactory.CreateMinimalJpeg()]);
        Assert.NotNull(submitBody);

        var flaggerSession = await ClaimTestHelpers.CreateAndLoginClaimantAsync(context);
        ClaimTestHelpers.Authenticate(context.Client, flaggerSession.AccessToken);
        var (_, flagBody) = await FlagListingAsync(context.Client, reportId, AbuseFlagReasons.Other);
        Assert.NotNull(flagBody);

        await HttpTestHelpers.LoginAsAdminAsync(context);

        var resolveResponse = await context.Client.PostAsJsonAsync(
            $"/api/v1/admin/abuse/{flagBody.Id}/resolve",
            new ResolveAbuseReportRequest { Outcome = AbuseResolutionOutcomes.NoAction });
        Assert.Equal(HttpStatusCode.OK, resolveResponse.StatusCode);

        var presignResponse = await context.Client.GetAsync(
            $"/api/v1/uploads/claim-photo/{submitBody.Id}/url");
        var presignError = await HttpTestHelpers.ReadErrorAsync(presignResponse);

        Assert.Equal(HttpStatusCode.Forbidden, presignResponse.StatusCode);
        Assert.Equal(ErrorCodes.AbuseInvestigationUnavailable, presignError?.Code);
    }

    [Fact]
    public async Task Admin_can_presign_private_report_photo_on_pending_report_without_flag()
    {
        await using var context = await ReportTestContext.CreateAsync(factory);
        var request = TestReportHelpers.BuildValidLostRequest(
            categoryCode: "documents-ids",
            categoryFields: new Dictionary<string, string>
            {
                ["document_type"] = "national_id",
                ["first_name_on_document"] = "Ahmed",
            });

        var (_, created) = await context.SubmitReportAsync(
            request,
            [TestImageFactory.CreateMinimalJpeg()]);
        Assert.NotNull(created);

        var (_, detail) = await context.GetReportAsync(created.Id);
        Assert.NotNull(detail);

        await HttpTestHelpers.LoginAsAdminAsync(context);

        var (response, body) = await context.GetPhotoUrlAsync(detail.Photos[0].Id);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body);
        Assert.StartsWith("https://fake.local/", body.Url, StringComparison.Ordinal);
    }

    private static async Task<(HttpResponseMessage Response, FlagListingResponse? Body)> FlagListingAsync(
        HttpClient client,
        Guid reportId,
        string reason)
    {
        var response = await client.PostAsJsonAsync(
            $"/api/v1/reports/{reportId}/flag",
            new FlagListingRequest { Reason = reason });

        FlagListingResponse? body = response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<FlagListingResponse>()
            : null;

        return (response, body);
    }
}
