using System.Net;
using Amanah.Api.Data.Entities;
using Amanah.Api.Tests.Infrastructure;
using Amanah.Api.Tests.Reports;
using Amanah.Api.Tests.Uploads;
using Amanah.Contracts.Errors;

namespace Amanah.Api.Tests.Browse;

public class PublicDetailTests(ApiWebApplicationFactory factory) : IClassFixture<ApiWebApplicationFactory>
{
    [Fact]
    public async Task Published_lost_report_is_accessible_via_lost_and_public_endpoints()
    {
        await using var context = await ReportTestContext.CreateAsync(factory);
        var client = BrowseTestHelpers.CreateAnonymousClient(factory);

        var reportId = await BrowseTestHelpers.SubmitAndPublishAsync(
            context,
            TestReportHelpers.BuildValidLostRequest(title: "Published lost wallet"));

        var (lostResponse, lostBody) = await BrowseTestHelpers.GetLostDetailAsync(client, reportId);
        var (publicResponse, publicBody) = await BrowseTestHelpers.GetPublicDetailAsync(client, reportId);

        Assert.Equal(HttpStatusCode.OK, lostResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, publicResponse.StatusCode);
        Assert.Equal("lost", lostBody?.Type);
        Assert.Equal("published", publicBody?.Status);
        Assert.Equal("Published lost wallet", publicBody?.Title);
    }

    [Fact]
    public async Task Published_found_report_is_accessible_via_found_endpoint()
    {
        await using var context = await ReportTestContext.CreateAsync(factory);
        var client = BrowseTestHelpers.CreateAnonymousClient(factory);

        var reportId = await BrowseTestHelpers.SubmitAndPublishAsync(
            context,
            TestReportHelpers.BuildValidFoundRequest(title: "Published found backpack"));

        var (response, body) = await BrowseTestHelpers.GetFoundDetailAsync(client, reportId);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("found", body?.Type);
        Assert.Equal("Published found backpack", body?.Title);
    }

    [Fact]
    public async Task Wrong_type_on_lost_url_returns_not_found()
    {
        await using var context = await ReportTestContext.CreateAsync(factory);
        var client = BrowseTestHelpers.CreateAnonymousClient(factory);

        var reportId = await BrowseTestHelpers.SubmitAndPublishAsync(
            context,
            TestReportHelpers.BuildValidFoundRequest(title: "Found-only item"));

        var (response, error) = await GetLostDetailWithErrorAsync(client, reportId);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(ErrorCodes.NotFound, error?.Code);
    }

    [Fact]
    public async Task Pending_and_rejected_reports_return_not_found()
    {
        await using var context = await ReportTestContext.CreateAsync(factory);
        var client = BrowseTestHelpers.CreateAnonymousClient(factory);

        var pendingId = await BrowseTestHelpers.SeedReportAsync(
            context,
            new BrowseTestHelpers.SeedReportOptions
            {
                Title = "Pending public detail item",
                Status = ReportStatus.PendingReview,
                PublishedAt = null,
            });
        var rejectedId = await BrowseTestHelpers.SeedReportAsync(
            context,
            new BrowseTestHelpers.SeedReportOptions
            {
                Title = "Rejected public detail item",
                Status = ReportStatus.Rejected,
                PublishedAt = null,
            });

        var (pendingResponse, pendingError) = await GetPublicDetailWithErrorAsync(client, pendingId);
        var (rejectedResponse, rejectedError) = await GetPublicDetailWithErrorAsync(client, rejectedId);

        Assert.Equal(HttpStatusCode.NotFound, pendingResponse.StatusCode);
        Assert.Equal(ErrorCodes.NotFound, pendingError?.Code);
        Assert.Equal(HttpStatusCode.NotFound, rejectedResponse.StatusCode);
        Assert.Equal(ErrorCodes.NotFound, rejectedError?.Code);
    }

    [Fact]
    public async Task Resolved_withdrawn_and_removed_reports_return_gone()
    {
        await using var context = await ReportTestContext.CreateAsync(factory);
        var client = BrowseTestHelpers.CreateAnonymousClient(factory);

        var resolvedId = await BrowseTestHelpers.SeedReportAsync(
            context,
            new BrowseTestHelpers.SeedReportOptions
            {
                Title = "Resolved public detail item",
                Status = ReportStatus.Resolved,
            });
        var withdrawnId = await BrowseTestHelpers.SeedReportAsync(
            context,
            new BrowseTestHelpers.SeedReportOptions
            {
                Title = "Withdrawn public detail item",
                Status = ReportStatus.Withdrawn,
            });
        var removedId = await BrowseTestHelpers.SeedReportAsync(
            context,
            new BrowseTestHelpers.SeedReportOptions
            {
                Title = "Removed public detail item",
                Status = ReportStatus.RemovedByAdmin,
            });

        var (resolvedResponse, resolvedError) = await GetLostDetailWithErrorAsync(client, resolvedId);
        var (withdrawnResponse, withdrawnError) = await GetLostDetailWithErrorAsync(client, withdrawnId);
        var (removedResponse, removedError) = await GetLostDetailWithErrorAsync(client, removedId);

        Assert.Equal(HttpStatusCode.Gone, resolvedResponse.StatusCode);
        Assert.Equal(ErrorCodes.Unavailable, resolvedError?.Code);
        Assert.Equal(HttpStatusCode.Gone, withdrawnResponse.StatusCode);
        Assert.Equal(ErrorCodes.Unavailable, withdrawnError?.Code);
        Assert.Equal(HttpStatusCode.Gone, removedResponse.StatusCode);
        Assert.Equal(ErrorCodes.Unavailable, removedError?.Code);
    }

    [Fact]
    public async Task Missing_id_returns_not_found()
    {
        await using var context = await ReportTestContext.CreateAsync(factory);
        var client = BrowseTestHelpers.CreateAnonymousClient(factory);

        var (response, error) = await GetLostDetailWithErrorAsync(client, Guid.NewGuid());

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(ErrorCodes.NotFound, error?.Code);
    }

    [Fact]
    public async Task Claim_in_progress_report_is_publicly_readable()
    {
        await using var context = await ReportTestContext.CreateAsync(factory);
        var client = BrowseTestHelpers.CreateAnonymousClient(factory);

        var reportId = await BrowseTestHelpers.SubmitAndPublishAsync(
            context,
            TestReportHelpers.BuildValidLostRequest(title: "Claim in progress item"));
        await BrowseTestHelpers.SetReportStatusAsync(
            context,
            reportId,
            ReportStatus.ClaimInProgress);

        var (response, body) = await BrowseTestHelpers.GetPublicDetailAsync(client, reportId);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("claim_in_progress", body?.Status);
    }

    [Fact]
    public async Task Public_detail_excludes_hidden_detail()
    {
        await using var context = await ReportTestContext.CreateAsync(factory);
        var client = BrowseTestHelpers.CreateAnonymousClient(factory);

        var reportId = await BrowseTestHelpers.SubmitAndPublishAsync(
            context,
            TestReportHelpers.BuildValidLostRequest(
                title: "Published hidden detail item",
                hiddenDetail: "Secret verification detail must not leak."));

        var response = await client.GetAsync($"/api/v1/reports/{reportId}/public");
        var json = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.DoesNotContain("hiddenDetail", json, StringComparison.Ordinal);
        Assert.DoesNotContain("Secret verification detail must not leak.", json, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Public_detail_returns_public_photo_urls_for_public_categories()
    {
        await using var context = await ReportTestContext.CreateAsync(factory);
        var client = BrowseTestHelpers.CreateAnonymousClient(factory);

        var (_, created) = await context.SubmitReportAsync(
            TestReportHelpers.BuildValidLostRequest(title: "Published phone with photo"),
            [TestImageFactory.CreateMinimalJpeg()]);
        Assert.NotNull(created);
        await HttpTestHelpers.ApproveAsAdminAsync(context, created.Id);

        var (response, body) = await BrowseTestHelpers.GetPublicDetailAsync(client, created.Id);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body);
        Assert.NotEmpty(body.Photos);
        Assert.StartsWith("https://fake.local/", body.Photos[0].ThumbnailUrl, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Public_detail_nulls_photos_for_private_category()
    {
        await using var context = await ReportTestContext.CreateAsync(factory);
        var client = BrowseTestHelpers.CreateAnonymousClient(factory);

        var reportId = await BrowseTestHelpers.SeedReportAsync(
            context,
            new BrowseTestHelpers.SeedReportOptions
            {
                CategoryCode = "documents-ids",
                Title = "Lost national ID",
                Description = "Lost my ID near the metro station.",
                CategoryFields = new Dictionary<string, string>
                {
                    ["document_type"] = "National ID",
                    ["first_name_on_document"] = "Ahmed",
                },
                WithPublicPhoto = true,
            });

        var (response, body) = await BrowseTestHelpers.GetPublicDetailAsync(client, reportId);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body);
        Assert.Single(body.Photos);
        Assert.Null(body.Photos[0].ThumbnailUrl);
    }

    private static async Task<(HttpResponseMessage Response, ApiError? Error)>
        GetPublicDetailWithErrorAsync(HttpClient client, Guid reportId)
    {
        var (response, _) = await BrowseTestHelpers.GetPublicDetailAsync(client, reportId);
        var error = await HttpTestHelpers.ReadErrorAsync(response);
        return (response, error);
    }

    private static async Task<(HttpResponseMessage Response, ApiError? Error)>
        GetLostDetailWithErrorAsync(HttpClient client, Guid reportId)
    {
        var (response, _) = await BrowseTestHelpers.GetLostDetailAsync(client, reportId);
        var error = await HttpTestHelpers.ReadErrorAsync(response);
        return (response, error);
    }
}
