using Amanah.Api.Tests.Infrastructure;
using Amanah.Contracts.Errors;

namespace Amanah.Api.Tests.Reports;

public class ReportAccessTests(ApiWebApplicationFactory factory) : IClassFixture<ApiWebApplicationFactory>
{
    [Fact]
    public async Task Get_own_pending_report_returns_detail()
    {
        await using var context = await ReportTestContext.CreateAsync(factory);
        var (_, created) = await context.SubmitReportAsync(TestReportHelpers.BuildValidLostRequest());
        Assert.NotNull(created);

        var (response, body) = await context.GetReportAsync(created.Id);

        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body);
        Assert.Equal(created.Id, body.Id);
        Assert.Equal("Lost black iPhone", body.Title);
    }

    [Fact]
    public async Task Get_pending_report_as_stranger_returns_not_found()
    {
        await using var reporterContext = await ReportTestContext.CreateAsync(factory);
        var (_, created) = await reporterContext.SubmitReportAsync(
            TestReportHelpers.BuildValidLostRequest());
        Assert.NotNull(created);

        await using var strangerContext = await ReportTestContext.CreateAsync(factory);
        var (response, error) = await GetReportWithErrorAsync(strangerContext, created.Id);

        Assert.Equal(System.Net.HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(ErrorCodes.NotFound, error?.Code);
    }

    [Fact]
    public async Task Get_mine_returns_only_pending_review_reports_for_caller()
    {
        await using var context = await ReportTestContext.CreateAsync(factory);
        var (_, first) = await context.SubmitReportAsync(TestReportHelpers.BuildValidLostRequest());
        var (_, second) = await context.SubmitReportAsync(
            TestReportHelpers.BuildValidLostRequest(title: "Lost black Samsung phone"));

        Assert.NotNull(first);
        Assert.NotNull(second);

        var (response, body) = await context.GetMineAsync("pending_review");

        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body);
        Assert.Equal(2, body.Items.Count);

        var (publishedResponse, publishedBody) = await context.GetMineAsync("published");
        Assert.Equal(System.Net.HttpStatusCode.OK, publishedResponse.StatusCode);
        Assert.NotNull(publishedBody);
        Assert.Empty(publishedBody.Items);
    }

    [Fact]
    public async Task Get_mine_rejects_invalid_status_filter()
    {
        await using var context = await ReportTestContext.CreateAsync(factory);

        var (response, error) = await GetMineWithErrorAsync(context, "not-a-status");

        Assert.Equal(System.Net.HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(ErrorCodes.ValidationFailed, error?.Code);
        Assert.Contains("status", error!.Errors!.Keys);
    }

    private static async Task<(HttpResponseMessage Response, ApiError? Error)> GetMineWithErrorAsync(
        ReportTestContext context,
        string status)
    {
        var (response, _) = await context.GetMineAsync(status);
        var error = await HttpTestHelpers.ReadErrorAsync(response);
        return (response, error);
    }

    private static async Task<(HttpResponseMessage Response, ApiError? Error)> GetReportWithErrorAsync(
        ReportTestContext context,
        Guid reportId)
    {
        var response = await context.Client.GetAsync($"/api/v1/reports/{reportId}");
        var error = await HttpTestHelpers.ReadErrorAsync(response);
        return (response, error);
    }
}
