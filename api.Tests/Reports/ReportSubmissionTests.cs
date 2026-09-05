using Amanah.Api.Data.Entities;
using Amanah.Api.Tests.Infrastructure;
using Amanah.Api.Tests.Uploads;
using Amanah.Api.Utilities.Common;
using Amanah.Api.Utilities.Reports;
using Amanah.Contracts.Errors;
using Microsoft.EntityFrameworkCore;

namespace Amanah.Api.Tests.Reports;

public class ReportSubmissionTests(ApiWebApplicationFactory factory) : IClassFixture<ApiWebApplicationFactory>
{
    [Fact]
    public async Task Create_lost_report_returns_pending_review_and_persists_fields()
    {
        await using var context = await ReportTestContext.CreateAsync(factory);
        var request = TestReportHelpers.BuildValidLostRequest();

        var (response, body) = await context.SubmitReportAsync(request);

        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body);
        Assert.Equal("pending_review", body.Status);

        var report = await context.DbContext.Reports
            .Include(report => report.CategoryFields)
            .SingleAsync(report => report.Id == body.Id);

        Assert.Equal(ReportStatus.PendingReview, report.Status);
        Assert.Equal(ReportType.Lost, report.Type);
        Assert.Equal(2, report.CategoryFields.Count);
        Assert.False(string.IsNullOrWhiteSpace(report.NormalizedSearchText));
    }

    [Fact]
    public async Task Create_found_report_persists_held_location()
    {
        await using var context = await ReportTestContext.CreateAsync(factory);
        var request = TestReportHelpers.BuildValidFoundRequest();

        var (response, body) = await context.SubmitReportAsync(request);

        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body);

        var report = await context.DbContext.Reports.SingleAsync(report => report.Id == body.Id);

        Assert.Equal("At Ramses police station", report.HeldLocation);
    }

    [Fact]
    public async Task Create_rejects_future_date()
    {
        await using var context = await ReportTestContext.CreateAsync(factory);
        var request = TestReportHelpers.BuildValidLostRequest(
            dateLostOrFound: CairoTime.TodayInCairo().AddDays(1));

        var (response, _) = await context.SubmitReportAsync(request);
        var error = await context.ReadErrorAsync(response);

        Assert.Equal(System.Net.HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(ErrorCodes.ValidationFailed, error?.Code);
        Assert.Contains(ReportDateValidator.FieldName, error!.Errors!.Keys);
    }

    [Fact]
    public async Task Create_rejects_contact_info_in_title()
    {
        await using var context = await ReportTestContext.CreateAsync(factory);
        var request = TestReportHelpers.BuildValidLostRequest(
            title: "Call me 01012345678 please");

        var (response, _) = await context.SubmitReportAsync(request);
        var error = await context.ReadErrorAsync(response);

        Assert.Equal(System.Net.HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains(
            ContactInfoDetector.ContactInfoMessage,
            error!.Errors![ReportContentValidator.TitleField]);
    }

    [Fact]
    public async Task Create_allows_contact_info_in_hidden_detail()
    {
        await using var context = await ReportTestContext.CreateAsync(factory);
        var request = TestReportHelpers.BuildValidLostRequest(
            hiddenDetail: "My backup number is 01012345678 inside.");

        var (response, body) = await context.SubmitReportAsync(request);

        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body);
    }

    [Fact]
    public async Task Create_rejects_missing_required_category_field()
    {
        await using var context = await ReportTestContext.CreateAsync(factory);
        var request = TestReportHelpers.BuildValidLostRequest(
            categoryFields: new Dictionary<string, string>
            {
                ["brand_model"] = "iPhone 14",
            });

        var (response, _) = await context.SubmitReportAsync(request);
        var error = await context.ReadErrorAsync(response);

        Assert.Equal(System.Net.HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("colour", error!.Errors!.Keys);
    }

    [Fact]
    public async Task Create_rejects_daily_quota_after_three_reports()
    {
        await using var context = await ReportTestContext.CreateAsync(factory);

        for (var i = 0; i < 3; i++)
        {
            var (okResponse, _) = await context.SubmitReportAsync(
                TestReportHelpers.BuildValidLostRequest(title: $"Lost black iPhone {i}"));
            Assert.Equal(System.Net.HttpStatusCode.OK, okResponse.StatusCode);
        }

        var (response, _) = await context.SubmitReportAsync(
            TestReportHelpers.BuildValidLostRequest(title: "Lost black iPhone 4"));
        var error = await context.ReadErrorAsync(response);

        Assert.Equal(System.Net.HttpStatusCode.TooManyRequests, response.StatusCode);
        Assert.Equal(ErrorCodes.ReportDailyQuota, error?.Code);
        Assert.True(response.Headers.RetryAfter is not null);
    }

    [Fact]
    public async Task Create_rejects_open_cap_after_five_open_reports()
    {
        await using var context = await ReportTestContext.CreateAsync(factory);
        var request = TestReportHelpers.BuildValidLostRequest();
        var yesterday = CairoTime.CairoDayStartUtc(DateTimeOffset.UtcNow).AddDays(-1);

        for (var i = 0; i < 5; i++)
        {
            context.DbContext.Reports.Add(new Report
            {
                Id = Guid.NewGuid(),
                ReporterId = context.Session.User.Id,
                Type = ReportType.Lost,
                CategoryId = await context.DbContext.Categories.Select(c => c.Id).FirstAsync(),
                GovernorateId = await context.DbContext.Governorates.Select(g => g.Id).FirstAsync(),
                Title = $"Existing open report number {i}",
                Description = "Detailed description of an existing open report.",
                DateLostOrFound = CairoTime.TodayInCairo(),
                Status = ReportStatus.Published,
                HiddenDetail = "Hidden verification detail for testing purposes.",
                CreatedAt = yesterday.AddHours(i + 1),
                UpdatedAt = yesterday.AddHours(i + 1),
            });
        }

        await context.DbContext.SaveChangesAsync();

        var (response, _) = await context.SubmitReportAsync(request);
        var error = await context.ReadErrorAsync(response);

        Assert.Equal(System.Net.HttpStatusCode.TooManyRequests, response.StatusCode);
        Assert.Equal(ErrorCodes.ReportOpenCap, error?.Code);
    }

    [Fact]
    public async Task Create_private_category_photos_hide_thumbnail_url()
    {
        await using var context = await ReportTestContext.CreateAsync(factory);
        var request = TestReportHelpers.BuildValidLostRequest(
            categoryCode: "documents-ids",
            categoryFields: new Dictionary<string, string>
            {
                ["document_type"] = "national_id",
                ["first_name_on_document"] = "Ahmed",
            });

        var (response, body) = await context.SubmitReportAsync(
            request,
            [TestImageFactory.CreateMinimalJpeg()]);

        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body);

        var (_, detail) = await context.GetReportAsync(body.Id);
        Assert.NotNull(detail);
        Assert.Single(detail.Photos);
        Assert.Null(detail.Photos[0].ThumbnailUrl);

        var (presignResponse, presignBody) = await context.GetPhotoUrlAsync(detail.Photos[0].Id);
        Assert.Equal(System.Net.HttpStatusCode.OK, presignResponse.StatusCode);
        Assert.NotNull(presignBody);
    }

    [Fact]
    public async Task Create_accepts_report_json_sent_as_file_part()
    {
        await using var context = await ReportTestContext.CreateAsync(factory);
        var request = TestReportHelpers.BuildValidLostRequest();

        var (response, body) = await context.SubmitReportAsync(
            request,
            photoContents: null,
            photoContentType: "image/jpeg",
            reportJsonAsFilePart: true);

        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body);
        Assert.Equal("pending_review", body.Status);
    }

    [Fact]
    public async Task Create_rejects_held_location_on_lost_report()
    {
        await using var context = await ReportTestContext.CreateAsync(factory);
        var request = TestReportHelpers.BuildValidLostRequest(
            heldLocation: "At Ramses police station");

        var (response, _) = await context.SubmitReportAsync(request);
        var error = await context.ReadErrorAsync(response);

        Assert.Equal(System.Net.HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains(ReportContentValidator.HeldLocationField, error!.Errors!.Keys);
    }
}
