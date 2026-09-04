using Amanah.Api.Data.Entities;
using Amanah.Api.Tests.Infrastructure;
using Amanah.Contracts.Errors;
using Amanah.Contracts.Requests.Reports;
using Microsoft.EntityFrameworkCore;

namespace Amanah.Api.Tests.Reports;

public class ReportWithdrawTests(ApiWebApplicationFactory factory) : IClassFixture<ApiWebApplicationFactory>
{
    [Fact]
    public async Task Withdraw_pending_report_sets_status_to_withdrawn()
    {
        await using var context = await ReportTestContext.CreateAsync(factory);
        var (_, created) = await context.SubmitReportAsync(TestReportHelpers.BuildValidLostRequest());
        Assert.NotNull(created);

        var response = await context.WithdrawReportAsync(
            created.Id,
            new() { Reason = "no_longer_needed" });

        Assert.Equal(System.Net.HttpStatusCode.NoContent, response.StatusCode);

        var report = await context.DbContext.Reports.SingleAsync(report => report.Id == created.Id);
        Assert.Equal(ReportStatus.Withdrawn, report.Status);
        Assert.Equal("no_longer_needed", report.WithdrawalReason);
    }

    [Fact]
    public async Task Withdraw_non_owned_report_returns_not_found()
    {
        await using var ownerContext = await ReportTestContext.CreateAsync(factory);
        var (_, created) = await ownerContext.SubmitReportAsync(TestReportHelpers.BuildValidLostRequest());
        Assert.NotNull(created);

        await using var otherContext = await ReportTestContext.CreateAsync(factory);
        var response = await otherContext.WithdrawReportAsync(created.Id);
        var error = await otherContext.ReadErrorAsync(response);

        Assert.Equal(System.Net.HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(ErrorCodes.NotFound, error?.Code);
    }

    [Fact]
    public async Task Withdraw_non_pending_report_returns_conflict()
    {
        await using var context = await ReportTestContext.CreateAsync(factory);
        var (_, created) = await context.SubmitReportAsync(TestReportHelpers.BuildValidLostRequest());
        Assert.NotNull(created);

        var report = await context.DbContext.Reports.SingleAsync(report => report.Id == created.Id);
        report.Status = ReportStatus.Published;
        await context.DbContext.SaveChangesAsync();

        var response = await context.WithdrawReportAsync(created.Id);
        var error = await context.ReadErrorAsync(response);

        Assert.Equal(System.Net.HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(ErrorCodes.Conflict, error?.Code);
    }
}
