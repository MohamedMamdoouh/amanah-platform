using System.Net;
using Amanah.Api.Data.Entities;
using Amanah.Api.Tests.Infrastructure;
using Amanah.Api.Tests.Reports;

namespace Amanah.Api.Tests.Browse;

public class BrowseSearchTests(ApiWebApplicationFactory factory) : IClassFixture<ApiWebApplicationFactory>
{
    [Fact]
    public async Task Search_matches_arabic_normalization_variants()
    {
        await using var context = await ReportTestContext.CreateAsync(factory);
        var client = BrowseTestHelpers.CreateAnonymousClient(factory);

        var reportId = await BrowseTestHelpers.SeedReportAsync(
            context,
            new BrowseTestHelpers.SeedReportOptions
            {
                Title = "حقيبة مدرسة زرقاء",
                Description = "فقدت الحقيبة بالقرب من المدرسة.",
            });

        var (taaMarbutaResponse, taaMarbutaBody) = await BrowseTestHelpers.GetBrowseAsync(
            client,
            "q=مدرسه");
        var (tatweelResponse, tatweelBody) = await BrowseTestHelpers.GetBrowseAsync(
            client,
            "q=حقيبه");
        var (diacriticsResponse, diacriticsBody) = await BrowseTestHelpers.GetBrowseAsync(
            client,
            "q=زرقاء");

        Assert.Equal(HttpStatusCode.OK, taaMarbutaResponse.StatusCode);
        Assert.Contains(taaMarbutaBody!.Items, item => item.Id == reportId);

        Assert.Equal(HttpStatusCode.OK, tatweelResponse.StatusCode);
        Assert.Contains(tatweelBody!.Items, item => item.Id == reportId);

        Assert.Equal(HttpStatusCode.OK, diacriticsResponse.StatusCode);
        Assert.Contains(diacriticsBody!.Items, item => item.Id == reportId);
    }

    [Fact]
    public async Task Search_requires_all_terms_AND()
    {
        await using var context = await ReportTestContext.CreateAsync(factory);
        var client = BrowseTestHelpers.CreateAnonymousClient(factory);

        var reportId = await BrowseTestHelpers.SeedReportAsync(
            context,
            new BrowseTestHelpers.SeedReportOptions
            {
                Title = "Blue leather wallet",
                Description = "Lost near Ramses station with cards inside.",
                AreaText = "Ramses station",
            });

        var (matchingResponse, matchingBody) = await BrowseTestHelpers.GetBrowseAsync(
            client,
            "q=blue wallet");
        var (missingTermResponse, missingTermBody) = await BrowseTestHelpers.GetBrowseAsync(
            client,
            "q=blue laptop");

        Assert.Equal(HttpStatusCode.OK, matchingResponse.StatusCode);
        Assert.Contains(matchingBody!.Items, item => item.Id == reportId);

        Assert.Equal(HttpStatusCode.OK, missingTermResponse.StatusCode);
        Assert.DoesNotContain(missingTermBody!.Items, item => item.Id == reportId);
    }

    [Fact]
    public async Task Search_with_empty_query_returns_all_browsable_reports()
    {
        await using var context = await ReportTestContext.CreateAsync(factory);
        var client = BrowseTestHelpers.CreateAnonymousClient(factory);

        var firstId = await BrowseTestHelpers.SeedReportAsync(
            context,
            new BrowseTestHelpers.SeedReportOptions { Title = "First browsable item" });
        var secondId = await BrowseTestHelpers.SeedReportAsync(
            context,
            new BrowseTestHelpers.SeedReportOptions { Title = "Second browsable item" });
        await BrowseTestHelpers.SeedReportAsync(
            context,
            new BrowseTestHelpers.SeedReportOptions
            {
                Title = "Pending only item",
                Status = ReportStatus.PendingReview,
            });

        var (response, body) = await BrowseTestHelpers.GetBrowseAsync(client, "q=");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body);
        Assert.Equal(2, body.TotalCount);
        Assert.Contains(body.Items, item => item.Id == firstId);
        Assert.Contains(body.Items, item => item.Id == secondId);
    }

    [Fact]
    public async Task Filters_combine_with_keyword_using_AND()
    {
        await using var context = await ReportTestContext.CreateAsync(factory);
        var client = BrowseTestHelpers.CreateAnonymousClient(factory);

        var matchingId = await BrowseTestHelpers.SeedReportAsync(
            context,
            new BrowseTestHelpers.SeedReportOptions
            {
                Type = ReportType.Lost,
                CategoryCode = "wallets",
                Title = "Blue leather wallet",
                Description = "Lost near Tahrir with cards inside.",
            });
        await BrowseTestHelpers.SeedReportAsync(
            context,
            new BrowseTestHelpers.SeedReportOptions
            {
                Type = ReportType.Found,
                CategoryCode = "wallets",
                Title = "Blue leather wallet found",
                Description = "Found near Tahrir with cards inside.",
            });
        await BrowseTestHelpers.SeedReportAsync(
            context,
            new BrowseTestHelpers.SeedReportOptions
            {
                Type = ReportType.Lost,
                CategoryCode = "phones",
                Title = "Blue iPhone",
                Description = "Lost phone near Tahrir.",
            });

        var (response, body) = await BrowseTestHelpers.GetBrowseAsync(
            client,
            "q=blue tahrir&category=wallets&type=lost");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body);
        Assert.Single(body.Items);
        Assert.Equal(matchingId, body.Items[0].Id);
    }
}
