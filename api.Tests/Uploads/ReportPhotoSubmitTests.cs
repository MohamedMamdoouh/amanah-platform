using Amanah.Api.Tests.Infrastructure;
using Amanah.Api.Tests.Reports;
using Amanah.Api.Utilities.Reports;
using Amanah.Contracts.Errors;
using Microsoft.EntityFrameworkCore;

namespace Amanah.Api.Tests.Uploads;

public class ReportPhotoSubmitTests(ApiWebApplicationFactory factory) : IClassFixture<ApiWebApplicationFactory>
{
    [Fact]
    public async Task Create_with_photos_persists_and_returns_detail_urls()
    {
        await using var context = await ReportTestContext.CreateAsync(factory);
        var request = TestReportHelpers.BuildValidLostRequest();

        var (response, body) = await context.SubmitReportAsync(
            request,
            [TestImageFactory.CreateMinimalJpeg()]);

        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body);

        var (_, detail) = await context.GetReportAsync(body.Id);
        Assert.NotNull(detail);
        Assert.Single(detail.Photos);
        Assert.NotNull(detail.Photos[0].ThumbnailUrl);
        Assert.StartsWith("https://fake.local/", detail.Photos[0].ThumbnailUrl, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Create_rejects_oversized_photo()
    {
        await using var context = await ReportTestContext.CreateAsync(factory);
        var request = TestReportHelpers.BuildValidLostRequest();

        var (response, error) = await context.SubmitReportAsync(
            request,
            [TestImageFactory.CreateOversizedJpeg()]);
        var apiError = await context.ReadErrorAsync(response);

        Assert.Equal(System.Net.HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(ErrorCodes.UploadTooLarge, apiError?.Code);
        Assert.Contains("photos[0]", apiError!.Errors!.Keys);
        Assert.Equal(0, await context.DbContext.Reports.CountAsync());
    }

    [Fact]
    public async Task Create_rejects_invalid_photo_format()
    {
        await using var context = await ReportTestContext.CreateAsync(factory);
        var request = TestReportHelpers.BuildValidLostRequest();

        var (response, _) = await context.SubmitReportAsync(
            request,
            ["not-an-image"u8.ToArray()],
            "image/jpeg");
        var apiError = await context.ReadErrorAsync(response);

        Assert.Equal(System.Net.HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(ErrorCodes.UploadInvalidFormat, apiError?.Code);
        Assert.Contains("photos[0]", apiError!.Errors!.Keys);
        Assert.Equal(0, await context.DbContext.Reports.CountAsync());
    }

    [Fact]
    public async Task Create_rejects_more_than_five_photos()
    {
        await using var context = await ReportTestContext.CreateAsync(factory);
        var request = TestReportHelpers.BuildValidLostRequest();
        var photos = Enumerable.Range(0, 6)
            .Select(_ => TestImageFactory.CreateMinimalJpeg())
            .ToArray();

        var (response, _) = await context.SubmitReportAsync(request, photos);
        var apiError = await context.ReadErrorAsync(response);

        Assert.Equal(System.Net.HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("photos", apiError!.Errors!.Keys);
    }

    [Fact]
    public async Task Create_rejects_invalid_second_photo_without_persisting_report()
    {
        await using var context = await ReportTestContext.CreateAsync(factory);
        var request = TestReportHelpers.BuildValidLostRequest();

        var (response, _) = await context.SubmitReportAsync(
            request,
            [TestImageFactory.CreateMinimalJpeg(), "not-an-image"u8.ToArray()],
            "image/jpeg");
        var apiError = await context.ReadErrorAsync(response);

        Assert.Equal(System.Net.HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(ErrorCodes.UploadInvalidFormat, apiError?.Code);
        Assert.Contains("photos[1]", apiError!.Errors!.Keys);
        Assert.Equal(0, await context.DbContext.Reports.CountAsync());
    }
}
