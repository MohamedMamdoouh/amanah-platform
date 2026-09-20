using System.Net;
using System.Net.Http.Json;
using Amanah.Api.Data.Entities;
using Amanah.Api.Tests.Browse;
using Amanah.Api.Tests.Claims;
using Amanah.Api.Tests.Infrastructure;
using Amanah.Api.Tests.Reports;
using Amanah.Api.Utilities.Abuse;
using Amanah.Contracts.Errors;
using Amanah.Contracts.Requests.Abuse;
using Amanah.Contracts.Responses.Abuse;
using Microsoft.EntityFrameworkCore;

namespace Amanah.Api.Tests.Abuse;

public class AbuseFlagTests(ApiWebApplicationFactory factory) : IClassFixture<ApiWebApplicationFactory>
{
    [Fact]
    public async Task Flag_published_listing_creates_open_flag()
    {
        await using var context = await ReportTestContext.CreateAsync(factory);
        var reportId = await ClaimTestHelpers.PublishLostReportAsync(context);
        var flaggerSession = await ClaimTestHelpers.CreateAndLoginClaimantAsync(context);
        ClaimTestHelpers.Authenticate(context.Client, flaggerSession.AccessToken);

        var (response, body) = await FlagListingAsync(
            context.Client,
            reportId,
            AbuseFlagReasons.ScamFraud,
            "Looks like a scam listing.");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body);
        Assert.Equal("open", body.Status);
        Assert.Equal(AbuseFlagReasons.ScamFraud, body.Reason);
        Assert.Equal(reportId, body.ReportId);

        var stored = await context.DbContext.AbuseReports
            .AsNoTracking()
            .SingleAsync(item => item.Id == body.Id);
        Assert.Equal(AbuseReportStatus.Open, stored.Status);
        Assert.Equal(flaggerSession.User.Id, stored.AbuseReporterId);
    }

    [Fact]
    public async Task Duplicate_flag_returns_conflict_and_get_returns_same_record()
    {
        await using var context = await ReportTestContext.CreateAsync(factory);
        var reportId = await ClaimTestHelpers.PublishLostReportAsync(context);
        var flaggerSession = await ClaimTestHelpers.CreateAndLoginClaimantAsync(context);
        ClaimTestHelpers.Authenticate(context.Client, flaggerSession.AccessToken);

        var (firstResponse, firstBody) = await FlagListingAsync(
            context.Client,
            reportId,
            AbuseFlagReasons.Spam);
        Assert.Equal(HttpStatusCode.OK, firstResponse.StatusCode);
        Assert.NotNull(firstBody);

        var (duplicateResponse, _) = await FlagListingAsync(
            context.Client,
            reportId,
            AbuseFlagReasons.Other);
        var duplicateError = await HttpTestHelpers.ReadErrorAsync(duplicateResponse);

        Assert.Equal(HttpStatusCode.Conflict, duplicateResponse.StatusCode);
        Assert.Equal(ErrorCodes.AbuseDuplicateFlag, duplicateError?.Code);

        var getResponse = await context.Client.GetAsync($"/api/v1/reports/{reportId}/flag");
        var getBody = await getResponse.Content.ReadFromJsonAsync<FlagListingResponse>();

        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        Assert.NotNull(getBody);
        Assert.Equal(firstBody.Id, getBody.Id);
        Assert.Equal(AbuseFlagReasons.Spam, getBody.Reason);
    }

    [Fact]
    public async Task Listing_owner_cannot_flag_own_listing()
    {
        await using var context = await ReportTestContext.CreateAsync(factory);
        var reportId = await ClaimTestHelpers.PublishLostReportAsync(context);

        var (response, _) = await FlagListingAsync(context.Client, reportId, AbuseFlagReasons.Spam);
        var error = await HttpTestHelpers.ReadErrorAsync(response);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(ErrorCodes.AbuseCannotFlagOwnListing, error?.Code);
        Assert.Equal(0, await context.DbContext.AbuseReports.CountAsync());
    }

    [Fact]
    public async Task Pending_review_listing_cannot_be_flagged()
    {
        await using var context = await ReportTestContext.CreateAsync(factory);
        var (_, created) = await context.SubmitReportAsync(TestReportHelpers.BuildValidLostRequest());
        Assert.NotNull(created);

        var flaggerSession = await ClaimTestHelpers.CreateAndLoginClaimantAsync(context);
        ClaimTestHelpers.Authenticate(context.Client, flaggerSession.AccessToken);

        var (response, _) = await FlagListingAsync(context.Client, created.Id, AbuseFlagReasons.Spam);
        var error = await HttpTestHelpers.ReadErrorAsync(response);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(ErrorCodes.AbuseListingNotFlaggable, error?.Code);
    }

    [Fact]
    public async Task Claim_in_progress_listing_can_be_flagged()
    {
        await using var context = await ReportTestContext.CreateAsync(factory);
        var reportId = await ClaimTestHelpers.PublishLostReportAsync(context);

        var claimantSession = await ClaimTestHelpers.CreateAndLoginClaimantAsync(context);
        ClaimTestHelpers.Authenticate(context.Client, claimantSession.AccessToken);
        var (claimResponse, claimBody) = await ClaimTestHelpers.SubmitClaimAsync(context.Client, reportId);
        Assert.Equal(HttpStatusCode.OK, claimResponse.StatusCode);
        Assert.NotNull(claimBody);

        ClaimTestHelpers.AuthenticateReporter(context.Client, context);
        var approveResponse = await ClaimTestHelpers.ApproveClaimAsync(context.Client, claimBody.Id);
        Assert.Equal(HttpStatusCode.NoContent, approveResponse.StatusCode);

        var flaggerSession = await ClaimTestHelpers.CreateAndLoginClaimantAsync(context);
        ClaimTestHelpers.Authenticate(context.Client, flaggerSession.AccessToken);

        var (flagResponse, flagBody) = await FlagListingAsync(
            context.Client,
            reportId,
            AbuseFlagReasons.HarassmentThreat);

        Assert.Equal(HttpStatusCode.OK, flagResponse.StatusCode);
        Assert.NotNull(flagBody);
        Assert.Equal("open", flagBody.Status);
    }

    [Fact]
    public async Task Flagged_listing_remains_visible_in_public_browse()
    {
        await using var context = await ReportTestContext.CreateAsync(factory);
        var reportId = await ClaimTestHelpers.PublishLostReportAsync(context);

        var flaggerSession = await ClaimTestHelpers.CreateAndLoginClaimantAsync(context);
        ClaimTestHelpers.Authenticate(context.Client, flaggerSession.AccessToken);
        var (flagResponse, _) = await FlagListingAsync(context.Client, reportId, AbuseFlagReasons.Spam);
        Assert.Equal(HttpStatusCode.OK, flagResponse.StatusCode);

        var anonymousClient = BrowseTestHelpers.CreateAnonymousClient(factory);
        var (_, browseBody) = await BrowseTestHelpers.GetBrowseAsync(anonymousClient);
        Assert.NotNull(browseBody);
        Assert.Contains(browseBody.Items, item => item.Id == reportId);
    }

    [Fact]
    public async Task Get_flag_without_open_flag_returns_not_found()
    {
        await using var context = await ReportTestContext.CreateAsync(factory);
        var reportId = await ClaimTestHelpers.PublishLostReportAsync(context);
        var flaggerSession = await ClaimTestHelpers.CreateAndLoginClaimantAsync(context);
        ClaimTestHelpers.Authenticate(context.Client, flaggerSession.AccessToken);

        var response = await context.Client.GetAsync($"/api/v1/reports/{reportId}/flag");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private static async Task<(HttpResponseMessage Response, FlagListingResponse? Body)> FlagListingAsync(
        HttpClient client,
        Guid reportId,
        string reason,
        string? note = null)
    {
        var response = await client.PostAsJsonAsync(
            $"/api/v1/reports/{reportId}/flag",
            new FlagListingRequest
            {
                Reason = reason,
                Note = note,
            });

        FlagListingResponse? body = response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<FlagListingResponse>()
            : null;

        return (response, body);
    }
}
