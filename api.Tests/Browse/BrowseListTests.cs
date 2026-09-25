using System.Net;
using Amanah.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Amanah.Api.Tests.Infrastructure;
using Amanah.Api.Tests.Reports;

namespace Amanah.Api.Tests.Browse;

public class BrowseListTests(ApiWebApplicationFactory factory) : IClassFixture<ApiWebApplicationFactory>
{
    [Fact]
    public async Task Browse_returns_only_published_and_claim_in_progress()
    {
        await using var context = await ReportTestContext.CreateAsync(factory);
        var client = BrowseTestHelpers.CreateAnonymousClient(factory);

        var publishedId = await BrowseTestHelpers.SeedReportAsync(
            context,
            new BrowseTestHelpers.SeedReportOptions
            {
                Title = "Published sapphire ring",
                Status = ReportStatus.Published,
            });
        var claimId = await BrowseTestHelpers.SeedReportAsync(
            context,
            new BrowseTestHelpers.SeedReportOptions
            {
                Title = "Claim in progress emerald watch",
                Status = ReportStatus.ClaimInProgress,
            });
        await BrowseTestHelpers.SeedReportAsync(
            context,
            new BrowseTestHelpers.SeedReportOptions
            {
                Title = "Pending ruby bracelet",
                Status = ReportStatus.PendingReview,
            });
        await BrowseTestHelpers.SeedReportAsync(
            context,
            new BrowseTestHelpers.SeedReportOptions
            {
                Title = "Rejected opal necklace",
                Status = ReportStatus.Rejected,
            });
        await BrowseTestHelpers.SeedReportAsync(
            context,
            new BrowseTestHelpers.SeedReportOptions
            {
                Title = "Resolved diamond earrings",
                Status = ReportStatus.Resolved,
            });

        var (response, body) = await BrowseTestHelpers.GetBrowseAsync(client);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body);
        Assert.Equal(2, body.TotalCount);
        Assert.Contains(body.Items, item => item.Id == publishedId);
        Assert.Contains(body.Items, item => item.Id == claimId);
        Assert.Equal("claim_in_progress", body.Items.Single(item => item.Id == claimId).Status);
    }

    [Fact]
    public async Task Browse_hides_the_signed_in_users_own_reports()
    {
        await using var context = await ReportTestContext.CreateAsync(factory);
        var (otherSession, _) = await context.Auth.RegisterNewUserAsync("01098765432");

        var ownId = await BrowseTestHelpers.SeedReportAsync(
            context,
            new BrowseTestHelpers.SeedReportOptions
            {
                Title = "Own published wallet",
            });
        var otherId = await BrowseTestHelpers.SeedReportAsync(
            context,
            new BrowseTestHelpers.SeedReportOptions
            {
                Title = "Someone else's published wallet",
            });

        var otherReport = await context.DbContext.Reports.SingleAsync(report => report.Id == otherId);
        otherReport.ReporterId = otherSession.User.Id;
        await context.DbContext.SaveChangesAsync();

        var (anonymousResponse, anonymousBody) = await BrowseTestHelpers.GetBrowseAsync(
            BrowseTestHelpers.CreateAnonymousClient(factory));
        Assert.Equal(HttpStatusCode.OK, anonymousResponse.StatusCode);
        Assert.NotNull(anonymousBody);
        Assert.Contains(anonymousBody.Items, item => item.Id == ownId);
        Assert.Contains(anonymousBody.Items, item => item.Id == otherId);

        var (ownerResponse, ownerBody) = await BrowseTestHelpers.GetBrowseAsync(context.Client);
        Assert.Equal(HttpStatusCode.OK, ownerResponse.StatusCode);
        Assert.NotNull(ownerBody);
        Assert.DoesNotContain(ownerBody.Items, item => item.Id == ownId);
        Assert.Contains(ownerBody.Items, item => item.Id == otherId);
    }

    [Fact]
    public async Task Browse_filters_by_category_governorate_type_and_date_range()
    {
        await using var context = await ReportTestContext.CreateAsync(factory);
        var client = BrowseTestHelpers.CreateAnonymousClient(factory);

        var matchingId = await BrowseTestHelpers.SeedReportAsync(
            context,
            new BrowseTestHelpers.SeedReportOptions
            {
                Type = ReportType.Lost,
                CategoryCode = "electronics",
                GovernorateCode = "alexandria",
                Title = "Lost electronics in Alexandria",
                DateLostOrFound = new DateOnly(2026, 8, 15),
            });
        await BrowseTestHelpers.SeedReportAsync(
            context,
            new BrowseTestHelpers.SeedReportOptions
            {
                Type = ReportType.Found,
                CategoryCode = "phones",
                GovernorateCode = "cairo",
                Title = "Found phone in Cairo",
                DateLostOrFound = new DateOnly(2026, 8, 20),
            });

        var (response, body) = await BrowseTestHelpers.GetBrowseAsync(
            client,
            "category=electronics&governorate=alexandria&type=lost&dateFrom=2026-08-01&dateTo=2026-08-31");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body);
        Assert.Single(body.Items);
        Assert.Equal(matchingId, body.Items[0].Id);
    }

    [Fact]
    public async Task Browse_accepts_type_filter_in_any_case()
    {
        await using var context = await ReportTestContext.CreateAsync(factory);
        var client = BrowseTestHelpers.CreateAnonymousClient(factory);

        var lostId = await BrowseTestHelpers.SeedReportAsync(
            context,
            new BrowseTestHelpers.SeedReportOptions
            {
                Type = ReportType.Lost,
                Title = "Lost wallet",
            });
        await BrowseTestHelpers.SeedReportAsync(
            context,
            new BrowseTestHelpers.SeedReportOptions
            {
                Type = ReportType.Found,
                Title = "Found keys",
            });

        var (response, body) = await BrowseTestHelpers.GetBrowseAsync(client, "type=Lost");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body);
        Assert.Single(body.Items);
        Assert.Equal(lostId, body.Items[0].Id);
    }

    [Fact]
    public async Task Browse_sorts_by_published_at_desc()
    {
        await using var context = await ReportTestContext.CreateAsync(factory);
        var client = BrowseTestHelpers.CreateAnonymousClient(factory);

        var olderId = await BrowseTestHelpers.SeedReportAsync(
            context,
            new BrowseTestHelpers.SeedReportOptions
            {
                Title = "Older published report",
                PublishedAt = new DateTimeOffset(2026, 8, 1, 12, 0, 0, TimeSpan.Zero),
            });
        var newerId = await BrowseTestHelpers.SeedReportAsync(
            context,
            new BrowseTestHelpers.SeedReportOptions
            {
                Title = "Newer published report",
                PublishedAt = new DateTimeOffset(2026, 9, 1, 12, 0, 0, TimeSpan.Zero),
            });

        var (response, body) = await BrowseTestHelpers.GetBrowseAsync(client);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body);
        Assert.Equal(newerId, body.Items[0].Id);
        Assert.Equal(olderId, body.Items[1].Id);
    }

    [Fact]
    public async Task Browse_paginates_results()
    {
        await using var context = await ReportTestContext.CreateAsync(factory);
        var client = BrowseTestHelpers.CreateAnonymousClient(factory);

        for (var i = 0; i < 21; i++)
        {
            await BrowseTestHelpers.SeedReportAsync(
                context,
                new BrowseTestHelpers.SeedReportOptions
                {
                    Title = $"Published browse item {i:D2}",
                    PublishedAt = new DateTimeOffset(2026, 9, 1, 0, i, 0, TimeSpan.Zero),
                });
        }

        var (firstPageResponse, firstPage) = await BrowseTestHelpers.GetBrowseAsync(
            client,
            "page=1&pageSize=20");
        var (secondPageResponse, secondPage) = await BrowseTestHelpers.GetBrowseAsync(
            client,
            "page=2&pageSize=20");

        Assert.Equal(HttpStatusCode.OK, firstPageResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, secondPageResponse.StatusCode);
        Assert.NotNull(firstPage);
        Assert.NotNull(secondPage);
        Assert.Equal(21, firstPage.TotalCount);
        Assert.Equal(2, firstPage.TotalPages);
        Assert.Equal(20, firstPage.Items.Count);
        Assert.Single(secondPage.Items);
        Assert.Equal(2, secondPage.Page);
    }

    [Fact]
    public async Task Browse_is_available_without_authentication()
    {
        await using var context = await ReportTestContext.CreateAsync(factory);
        var anonymousClient = BrowseTestHelpers.CreateAnonymousClient(factory);

        var publishedId = await BrowseTestHelpers.SubmitAndPublishAsync(
            context,
            TestReportHelpers.BuildValidLostRequest(title: "Anonymous browse phone"));

        var (response, body) = await BrowseTestHelpers.GetBrowseAsync(anonymousClient);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body);
        Assert.Contains(body.Items, item => item.Id == publishedId);
    }
}
