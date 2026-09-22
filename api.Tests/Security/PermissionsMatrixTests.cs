using System.Net;
using System.Net.Http.Json;
using Amanah.Api.Tests.Browse;
using Amanah.Api.Tests.Chats;
using Amanah.Api.Tests.Claims;
using Amanah.Api.Tests.Infrastructure;
using Amanah.Api.Tests.Reports;
using Amanah.Api.Tests.Resolution;
using Amanah.Api.Tests.Uploads;
using Amanah.Api.Utilities.Abuse;
using Amanah.Contracts.Errors;
using Amanah.Contracts.Requests.Abuse;
using Amanah.Contracts.Requests.Reports;
using Amanah.Contracts.Responses.Abuse;
using Amanah.Contracts.Responses.Admin;
using Amanah.Contracts.Responses.Browse;
using Amanah.Contracts.Responses.Chats;
using Amanah.Contracts.Responses.Reports;
using Microsoft.EntityFrameworkCore;

namespace Amanah.Api.Tests.Security;

public class PermissionsMatrixTests(ApiWebApplicationFactory factory) : IClassFixture<ApiWebApplicationFactory>
{
    [Fact]
    public async Task Public_browse_list_nulls_private_category_thumbnail()
    {
        await using var context = await ReportTestContext.CreateAsync(factory);
        var client = BrowseTestHelpers.CreateAnonymousClient(factory);

        var reportId = await BrowseTestHelpers.SeedReportAsync(
            context,
            new BrowseTestHelpers.SeedReportOptions
            {
                CategoryCode = "documents-ids",
                Title = "Lost national ID browse matrix",
                WithPublicPhoto = true,
            });

        var (response, body) = await BrowseTestHelpers.GetBrowseAsync(client);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body);
        var item = Assert.Single(body.Items, row => row.Id == reportId);
        Assert.Null(item.ThumbnailUrl);
    }

    [Fact]
    public async Task Pending_claimant_cannot_presign_private_report_photo()
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
        await HttpTestHelpers.ApproveAsAdminAsync(context, created.Id);

        var (_, detail) = await context.GetReportAsync(created.Id);
        Assert.NotNull(detail);
        var privatePhotoId = detail.Photos[0].Id;

        var claimantSession = await ClaimTestHelpers.CreateAndLoginClaimantAsync(context);
        ClaimTestHelpers.Authenticate(context.Client, claimantSession.AccessToken);
        await ClaimTestHelpers.SubmitClaimAsync(context.Client, created.Id);

        var presignResponse = await context.Client.GetAsync(
            $"/api/v1/uploads/report-photo/{privatePhotoId}/url");
        var error = await HttpTestHelpers.ReadErrorAsync(presignResponse);

        Assert.Equal(HttpStatusCode.NotFound, presignResponse.StatusCode);
        Assert.Equal(ErrorCodes.NotFound, error?.Code);
    }

    [Fact]
    public async Task Logged_in_stranger_public_detail_omits_hidden_detail_and_private_photo_urls()
    {
        await using var context = await ReportTestContext.CreateAsync(factory);
        var (_, created) = await context.SubmitReportAsync(
            TestReportHelpers.BuildValidLostRequest(
                title: "Matrix public detail",
                hiddenDetail: "Secret matrix hidden detail text.",
                categoryCode: "documents-ids",
                categoryFields: new Dictionary<string, string>
                {
                    ["document_type"] = "national_id",
                    ["first_name_on_document"] = "Ahmed",
                }),
            [TestImageFactory.CreateMinimalJpeg()]);
        Assert.NotNull(created);

        var reportId = created.Id;
        await HttpTestHelpers.ApproveAsAdminAsync(context, reportId);

        var strangerSession = await ClaimTestHelpers.CreateAndLoginClaimantAsync(context);
        ClaimTestHelpers.Authenticate(context.Client, strangerSession.AccessToken);

        var response = await context.Client.GetAsync($"/api/v1/reports/{reportId}/public");
        var json = await response.Content.ReadAsStringAsync();
        var body = await response.Content.ReadFromJsonAsync<PublicReportDetailResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body);
        Assert.DoesNotContain("hiddenDetail", json, StringComparison.Ordinal);
        Assert.DoesNotContain("Secret matrix hidden detail text.", json, StringComparison.Ordinal);
        Assert.NotEmpty(body.Photos);
        Assert.Null(body.Photos[0].ThumbnailUrl);
    }

    [Fact]
    public async Task Cross_user_claim_chat_and_public_responses_never_include_counterparty_phone()
    {
        await using var context = await ReportTestContext.CreateAsync(factory);
        var reportId = await ClaimTestHelpers.PublishLostReportAsync(context);

        var reporterPhone = await context.DbContext.Users
            .AsNoTracking()
            .Where(user => user.Id == context.Session.User.Id)
            .Select(user => user.NormalizedPhone)
            .SingleAsync();

        var claimantSession = await ClaimTestHelpers.CreateAndLoginClaimantAsync(context);
        var claimantPhone = claimantSession.User.Phone;
        ClaimTestHelpers.Authenticate(context.Client, claimantSession.AccessToken);

        var (_, submitted) = await ClaimTestHelpers.SubmitClaimAsync(context.Client, reportId);
        Assert.NotNull(submitted);

        ClaimTestHelpers.AuthenticateReporter(context.Client, context);
        await ClaimTestHelpers.ApproveClaimAsync(context.Client, submitted.Id);

        var threadId = await ChatTestHelpers.GetThreadIdAsync(context.Client, submitted.Id);

        ClaimTestHelpers.Authenticate(context.Client, claimantSession.AccessToken);

        var (claimResponse, _) = await ClaimTestHelpers.GetClaimAsync(context.Client, submitted.Id);
        var claimJson = await claimResponse.Content.ReadAsStringAsync();
        AssertPhoneSecretsExcluded(claimJson, reporterPhone);

        var publicResponse = await context.Client.GetAsync($"/api/v1/reports/{reportId}/public");
        var publicJson = await publicResponse.Content.ReadAsStringAsync();
        AssertPhoneSecretsExcluded(publicJson, reporterPhone);

        var listResponse = await ChatTestHelpers.ListChatsAsync(context.Client);
        var listJson = await listResponse.Content.ReadAsStringAsync();
        AssertPhoneSecretsExcluded(listJson, reporterPhone);

        var threadResponse = await ChatTestHelpers.GetThreadAsync(context.Client, threadId);
        var threadJson = await threadResponse.Content.ReadAsStringAsync();
        AssertPhoneSecretsExcluded(threadJson, reporterPhone);

        ClaimTestHelpers.AuthenticateReporter(context.Client, context);

        var reporterClaimsResponse = await ClaimTestHelpers.GetReportClaimsAsync(context.Client, reportId);
        var reporterClaimsJson = await reporterClaimsResponse.Response.Content.ReadAsStringAsync();
        AssertPhoneSecretsExcluded(reporterClaimsJson, claimantPhone);
    }

    [Fact]
    public async Task Admin_cannot_read_claim_detail_without_being_participant()
    {
        await using var context = await ReportTestContext.CreateAsync(factory);
        var reportId = await ClaimTestHelpers.PublishLostReportAsync(context);

        var claimantSession = await ClaimTestHelpers.CreateAndLoginClaimantAsync(context);
        ClaimTestHelpers.Authenticate(context.Client, claimantSession.AccessToken);
        var (_, submitted) = await ClaimTestHelpers.SubmitClaimAsync(context.Client, reportId);
        Assert.NotNull(submitted);

        await HttpTestHelpers.LoginAsAdminAsync(context);

        var response = await context.Client.GetAsync($"/api/v1/claims/{submitted.Id}");
        var error = await HttpTestHelpers.ReadErrorAsync(response);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(ErrorCodes.NotFound, error?.Code);
    }

    [Fact]
    public async Task Admin_investigation_claims_requires_open_flag()
    {
        await using var context = await ReportTestContext.CreateAsync(factory);
        var reportId = await ClaimTestHelpers.PublishLostReportAsync(context);

        var claimantSession = await ClaimTestHelpers.CreateAndLoginClaimantAsync(context);
        ClaimTestHelpers.Authenticate(context.Client, claimantSession.AccessToken);
        await ClaimTestHelpers.SubmitClaimAsync(context.Client, reportId);

        await HttpTestHelpers.LoginAsAdminAsync(context);

        var response = await context.Client.GetAsync(
            $"/api/v1/admin/investigations/{reportId}/claims");
        var error = await HttpTestHelpers.ReadErrorAsync(response);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(ErrorCodes.AbuseInvestigationUnavailable, error?.Code);
    }

    [Fact]
    public async Task Admin_investigation_claims_exposes_claim_text_during_open_flag()
    {
        await using var context = await ReportTestContext.CreateAsync(factory);
        var reportId = await ClaimTestHelpers.PublishLostReportAsync(context);

        var claimantSession = await ClaimTestHelpers.CreateAndLoginClaimantAsync(context);
        ClaimTestHelpers.Authenticate(context.Client, claimantSession.AccessToken);
        var (_, submitted) = await ClaimTestHelpers.SubmitClaimAsync(
            context.Client,
            reportId,
            "Unique matrix claim answer for investigation.");
        Assert.NotNull(submitted);

        var flaggerSession = await ClaimTestHelpers.CreateAndLoginClaimantAsync(context);
        ClaimTestHelpers.Authenticate(context.Client, flaggerSession.AccessToken);
        await FlagListingAsync(context.Client, reportId, AbuseFlagReasons.Other);

        await HttpTestHelpers.LoginAsAdminAsync(context);

        var response = await context.Client.GetAsync(
            $"/api/v1/admin/investigations/{reportId}/claims");
        var body = await response.Content.ReadFromJsonAsync<InvestigationClaimsResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body);
        var claim = Assert.Single(body.Items);
        Assert.Equal(submitted.Id, claim.Id);
        Assert.Equal("Unique matrix claim answer for investigation.", claim.SubmittedAnswer);
    }

    [Fact]
    public async Task Admin_published_private_report_photo_presign_requires_open_investigation()
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
        await HttpTestHelpers.ApproveAsAdminAsync(context, created.Id);

        var (_, detail) = await context.GetReportAsync(created.Id);
        Assert.NotNull(detail);

        await HttpTestHelpers.LoginAsAdminAsync(context);

        var blockedResponse = await context.Client.GetAsync(
            $"/api/v1/uploads/report-photo/{detail.Photos[0].Id}/url");
        var blockedError = await HttpTestHelpers.ReadErrorAsync(blockedResponse);

        Assert.Equal(HttpStatusCode.NotFound, blockedResponse.StatusCode);
        Assert.Equal(ErrorCodes.NotFound, blockedError?.Code);
    }

    [Fact]
    public async Task Admin_chat_list_excludes_non_participant_threads()
    {
        await using var context = await ReportTestContext.CreateAsync(factory);
        var scenario = await ResolutionTestHelpers.CreateApprovedClaimScenarioAsync(context);
        _ = await ChatTestHelpers.GetThreadIdAsync(context.Client, scenario.ClaimId);

        await HttpTestHelpers.LoginAsAdminAsync(context);

        var listResponse = await ChatTestHelpers.ListChatsAsync(context.Client);
        var list = await listResponse.Content.ReadFromJsonAsync<ChatThreadListResponse>();

        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
        Assert.NotNull(list);
        Assert.Empty(list.Items);
    }

    [Fact]
    public async Task Withdrawal_reason_visible_to_reporter_and_admin_not_via_public_detail()
    {
        await using var context = await ReportTestContext.CreateAsync(factory);
        var reportId = await ClaimTestHelpers.PublishLostReportAsync(context);

        ClaimTestHelpers.AuthenticateReporter(context.Client, context);
        var withdrawResponse = await context.WithdrawReportAsync(
            reportId,
            new() { Reason = "recovered_outside" });
        Assert.Equal(HttpStatusCode.NoContent, withdrawResponse.StatusCode);

        var (reporterDetailResponse, reporterDetail) = await context.GetReportAsync(reportId);
        Assert.Equal(HttpStatusCode.OK, reporterDetailResponse.StatusCode);
        Assert.Equal("recovered_outside", reporterDetail?.WithdrawalReason);

        await HttpTestHelpers.LoginAsAdminAsync(context);
        var adminModerationResponse = await context.Client.GetAsync(
            $"/api/v1/admin/moderation/reports/{reportId}");
        var adminDetail = await adminModerationResponse.Content.ReadFromJsonAsync<ReportDetailResponse>();

        Assert.Equal(HttpStatusCode.OK, adminModerationResponse.StatusCode);
        Assert.Equal("recovered_outside", adminDetail?.WithdrawalReason);

        var client = BrowseTestHelpers.CreateAnonymousClient(factory);
        var (publicResponse, publicError) = await GetPublicDetailWithErrorAsync(client, reportId);

        Assert.Equal(HttpStatusCode.Gone, publicResponse.StatusCode);
        Assert.Equal(ErrorCodes.Unavailable, publicError?.Code);
    }

    private static void AssertPhoneSecretsExcluded(string json, string normalizedPhone)
    {
        Assert.DoesNotContain(normalizedPhone, json, StringComparison.Ordinal);

        if (normalizedPhone.StartsWith("+20", StringComparison.Ordinal) && normalizedPhone.Length > 3)
        {
            var local = "0" + normalizedPhone[3..];
            Assert.DoesNotContain(local, json, StringComparison.Ordinal);
        }
    }

    private static async Task<(HttpResponseMessage Response, ApiError? Error)> GetPublicDetailWithErrorAsync(
        HttpClient client,
        Guid reportId)
    {
        var (response, _) = await BrowseTestHelpers.GetPublicDetailAsync(client, reportId);
        var error = await HttpTestHelpers.ReadErrorAsync(response);
        return (response, error);
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
