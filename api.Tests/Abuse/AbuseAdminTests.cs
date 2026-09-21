using System.Net;
using System.Net.Http.Json;
using Amanah.Api.Data.Entities;
using Amanah.Api.Tests.Claims;
using Amanah.Api.Tests.Infrastructure;
using Amanah.Api.Tests.Reports;
using Amanah.Api.Utilities.Abuse;
using Amanah.Api.Utilities.Notifications;
using Amanah.Contracts.Errors;
using Amanah.Contracts.Requests.Abuse;
using Amanah.Contracts.Requests.Admin;
using Amanah.Contracts.Responses.Abuse;
using Amanah.Contracts.Responses.Admin;
using Microsoft.EntityFrameworkCore;

namespace Amanah.Api.Tests.Abuse;

public class AbuseAdminTests(ApiWebApplicationFactory factory) : IClassFixture<ApiWebApplicationFactory>
{
    [Fact]
    public async Task Queue_lists_open_flags_fifo_with_explicit_reporter_and_listing_fields()
    {
        await using var context = await ReportTestContext.CreateAsync(factory);
        var firstReportId = await ClaimTestHelpers.PublishLostReportAsync(context);

        var secondReporterSession = await ClaimTestHelpers.CreateAndLoginClaimantAsync(context);
        ClaimTestHelpers.Authenticate(context.Client, secondReporterSession.AccessToken);
        var (_, secondCreated) = await context.SubmitReportAsync(TestReportHelpers.BuildValidLostRequest());
        Assert.NotNull(secondCreated);
        await HttpTestHelpers.ApproveAsAdminAsync(context, secondCreated.Id);
        var secondReportId = secondCreated.Id;

        var firstFlaggerSession = await ClaimTestHelpers.CreateAndLoginClaimantAsync(context);
        ClaimTestHelpers.Authenticate(context.Client, firstFlaggerSession.AccessToken);
        var (_, firstFlag) = await FlagListingAsync(
            context.Client,
            firstReportId,
            AbuseFlagReasons.Spam);
        Assert.NotNull(firstFlag);

        await Task.Delay(15);

        var secondFlaggerSession = await ClaimTestHelpers.CreateAndLoginClaimantAsync(context);
        ClaimTestHelpers.Authenticate(context.Client, secondFlaggerSession.AccessToken);
        var (_, secondFlag) = await FlagListingAsync(
            context.Client,
            secondReportId,
            AbuseFlagReasons.ScamFraud);
        Assert.NotNull(secondFlag);

        await HttpTestHelpers.LoginAsAdminAsync(context);

        var queueResponse = await context.Client.GetAsync("/api/v1/admin/abuse");
        var queueBody = await queueResponse.Content.ReadFromJsonAsync<AbuseQueueResponse>();

        Assert.Equal(HttpStatusCode.OK, queueResponse.StatusCode);
        Assert.NotNull(queueBody);
        Assert.Equal(2, queueBody.OpenCount);
        Assert.Equal(2, queueBody.Items.Count);
        Assert.Equal(firstFlag.Id, queueBody.Items[0].Id);
        Assert.Equal(secondFlag.Id, queueBody.Items[1].Id);

        var firstItem = queueBody.Items[0];
        Assert.Equal(firstFlaggerSession.User.Id, firstItem.AbuseReporterUserId);
        Assert.Equal(firstFlaggerSession.User.DisplayName, firstItem.AbuseReporterDisplayName);
        Assert.Equal(firstReportId, firstItem.ReportId);
        Assert.Equal("published", firstItem.ReportStatus);
        Assert.Equal("open", firstItem.Status);

        var detailResponse = await context.Client.GetAsync($"/api/v1/admin/abuse/{firstFlag.Id}");
        var detailBody = await detailResponse.Content.ReadFromJsonAsync<AbuseReportDetailResponse>();

        Assert.Equal(HttpStatusCode.OK, detailResponse.StatusCode);
        Assert.NotNull(detailBody);
        Assert.Equal(context.Session.User.Id, detailBody.Listing.ListingOwnerUserId);
        Assert.Equal(context.Session.User.DisplayName, detailBody.Listing.ListingOwnerDisplayName);
        Assert.Equal(firstFlaggerSession.User.Id, detailBody.AbuseReporterUserId);
    }

    [Fact]
    public async Task Resolve_no_action_marks_flag_resolved_and_notifies_flagger_without_admin_note()
    {
        await using var context = await ReportTestContext.CreateAsync(factory);
        var (abuseReportId, flaggerUserId) = await SeedOpenFlagAsync(context);

        await HttpTestHelpers.LoginAsAdminAsync(context);

        var response = await context.Client.PostAsJsonAsync(
            $"/api/v1/admin/abuse/{abuseReportId}/resolve",
            new ResolveAbuseReportRequest
            {
                Outcome = AbuseResolutionOutcomes.NoAction,
                AdminNote = "Internal-only review note",
            });
        var body = await response.Content.ReadFromJsonAsync<ResolveAbuseReportResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body);
        Assert.Equal("resolved", body.Status);
        Assert.Equal(AbuseResolutionOutcomes.NoAction, body.ResolutionOutcome);

        var stored = await context.DbContext.AbuseReports
            .AsNoTracking()
            .SingleAsync(item => item.Id == abuseReportId);
        Assert.Equal(AbuseReportStatus.Resolved, stored.Status);

        var notification = await context.DbContext.Notifications
            .AsNoTracking()
            .SingleAsync(item =>
                item.UserId == flaggerUserId
                && item.Type == NotificationTypes.AbuseReportResolvedForFlagger);
        Assert.Contains(AbuseResolutionOutcomes.NoAction, notification.PayloadJson, StringComparison.Ordinal);
        Assert.DoesNotContain("Internal-only review note", notification.PayloadJson, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Resolve_takedown_removes_listing_and_records_outcome()
    {
        await using var context = await ReportTestContext.CreateAsync(factory);
        var reportId = await ClaimTestHelpers.PublishLostReportAsync(context);
        var (abuseReportId, _) = await SeedOpenFlagAsync(context, reportId);

        await HttpTestHelpers.LoginAsAdminAsync(context);

        var response = await context.Client.PostAsJsonAsync(
            $"/api/v1/admin/abuse/{abuseReportId}/resolve",
            new ResolveAbuseReportRequest
            {
                Outcome = AbuseResolutionOutcomes.Takedown,
                AdminNote = "Confirmed policy violation",
            });
        var body = await response.Content.ReadFromJsonAsync<ResolveAbuseReportResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body);
        Assert.Equal(AbuseResolutionOutcomes.Takedown, body.ResolutionOutcome);

        var report = await context.DbContext.Reports
            .AsNoTracking()
            .SingleAsync(item => item.Id == reportId);
        Assert.Equal(ReportStatus.RemovedByAdmin, report.Status);
    }

    [Fact]
    public async Task Resolve_ban_defaults_to_listing_owner_when_ban_target_omitted()
    {
        await using var context = await ReportTestContext.CreateAsync(factory);
        var reportId = await ClaimTestHelpers.PublishLostReportAsync(context);
        var listingOwnerId = context.Session.User.Id;
        var (abuseReportId, _) = await SeedOpenFlagAsync(context, reportId);

        await HttpTestHelpers.LoginAsAdminAsync(context);

        var response = await context.Client.PostAsJsonAsync(
            $"/api/v1/admin/abuse/{abuseReportId}/resolve",
            new ResolveAbuseReportRequest
            {
                Outcome = AbuseResolutionOutcomes.Ban,
                AdminNote = "Repeated scam listings",
            });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var user = await context.DbContext.Users
            .AsNoTracking()
            .SingleAsync(item => item.Id == listingOwnerId);
        Assert.True(user.IsBanned);
        Assert.Equal("Repeated scam listings", user.BanReason);
    }

    [Fact]
    public async Task Resolve_ban_honors_explicit_ban_target_user_id()
    {
        await using var context = await ReportTestContext.CreateAsync(factory);
        var reportId = await ClaimTestHelpers.PublishLostReportAsync(context);
        var (abuseReportId, flaggerUserId) = await SeedOpenFlagAsync(context, reportId);

        await HttpTestHelpers.LoginAsAdminAsync(context);

        var response = await context.Client.PostAsJsonAsync(
            $"/api/v1/admin/abuse/{abuseReportId}/resolve",
            new ResolveAbuseReportRequest
            {
                Outcome = AbuseResolutionOutcomes.Ban,
                AdminNote = "Malicious flagging pattern",
                BanTargetUserId = flaggerUserId,
            });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var flagger = await context.DbContext.Users
            .AsNoTracking()
            .SingleAsync(item => item.Id == flaggerUserId);
        Assert.True(flagger.IsBanned);

        var listingOwner = await context.DbContext.Users
            .AsNoTracking()
            .SingleAsync(item => item.Id == context.Session.User.Id);
        Assert.False(listingOwner.IsBanned);
    }

    [Fact]
    public async Task Non_admin_cannot_access_abuse_queue()
    {
        await using var context = await ReportTestContext.CreateAsync(factory);
        await ClaimTestHelpers.PublishLostReportAsync(context);

        var response = await context.Client.GetAsync("/api/v1/admin/abuse");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Resolve_already_resolved_flag_returns_conflict()
    {
        await using var context = await ReportTestContext.CreateAsync(factory);
        var (abuseReportId, _) = await SeedOpenFlagAsync(context);

        await HttpTestHelpers.LoginAsAdminAsync(context);

        var firstResponse = await context.Client.PostAsJsonAsync(
            $"/api/v1/admin/abuse/{abuseReportId}/resolve",
            new ResolveAbuseReportRequest { Outcome = AbuseResolutionOutcomes.NoAction });
        Assert.Equal(HttpStatusCode.OK, firstResponse.StatusCode);

        var secondResponse = await context.Client.PostAsJsonAsync(
            $"/api/v1/admin/abuse/{abuseReportId}/resolve",
            new ResolveAbuseReportRequest { Outcome = AbuseResolutionOutcomes.NoAction });
        var error = await HttpTestHelpers.ReadErrorAsync(secondResponse);

        Assert.Equal(HttpStatusCode.Conflict, secondResponse.StatusCode);
        Assert.Equal(ErrorCodes.AbuseAlreadyResolved, error?.Code);
    }

    private static async Task<(Guid AbuseReportId, Guid FlaggerUserId)> SeedOpenFlagAsync(
        ReportTestContext context,
        Guid? reportId = null)
    {
        var listingId = reportId ?? await ClaimTestHelpers.PublishLostReportAsync(context);
        var flaggerSession = await ClaimTestHelpers.CreateAndLoginClaimantAsync(context);
        ClaimTestHelpers.Authenticate(context.Client, flaggerSession.AccessToken);

        var (_, flagBody) = await FlagListingAsync(
            context.Client,
            listingId,
            AbuseFlagReasons.Other);

        Assert.NotNull(flagBody);
        return (flagBody.Id, flaggerSession.User.Id);
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
