using System.Net.Http.Headers;
using Amanah.Api.Tests.Infrastructure;
using Amanah.Api.Tests.Reports;
using Amanah.Contracts.Errors;

namespace Amanah.Api.Tests.Uploads;

public class ReportPhotoPresignTests(ApiWebApplicationFactory factory) : IClassFixture<ApiWebApplicationFactory>
{
    [Fact]
    public async Task Reporter_can_presign_own_private_photo()
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

        var (_, detail) = await context.GetReportAsync(created.Id);
        Assert.NotNull(detail);
        Assert.Single(detail.Photos);
        Assert.Null(detail.Photos[0].ThumbnailUrl);

        var (response, body) = await context.GetPhotoUrlAsync(detail.Photos[0].Id);

        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body);
        Assert.StartsWith("https://fake.local/", body.Url, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Public_category_photo_presign_returns_not_found()
    {
        await using var context = await ReportTestContext.CreateAsync(factory);
        var request = TestReportHelpers.BuildValidLostRequest();

        var (_, created) = await context.SubmitReportAsync(
            request,
            [TestImageFactory.CreateMinimalJpeg()]);
        Assert.NotNull(created);

        var (_, detail) = await context.GetReportAsync(created.Id);
        Assert.NotNull(detail);
        Assert.NotNull(detail.Photos[0].ThumbnailUrl);

        var (response, error) = await GetPhotoUrlWithErrorAsync(context, detail.Photos[0].Id);

        Assert.Equal(System.Net.HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(ErrorCodes.NotFound, error?.Code);
    }

    [Fact]
    public async Task Stranger_cannot_presign_private_photo()
    {
        await using var reporterContext = await ReportTestContext.CreateAsync(factory);
        var request = TestReportHelpers.BuildValidLostRequest(
            categoryCode: "documents-ids",
            categoryFields: new Dictionary<string, string>
            {
                ["document_type"] = "national_id",
                ["first_name_on_document"] = "Ahmed",
            });

        var (_, created) = await reporterContext.SubmitReportAsync(
            request,
            [TestImageFactory.CreateMinimalJpeg()]);
        Assert.NotNull(created);

        var (_, detail) = await reporterContext.GetReportAsync(created.Id);
        Assert.NotNull(detail);

        await using var strangerContext = await ReportTestContext.CreateAsync(factory);
        var (response, error) = await GetPhotoUrlWithErrorAsync(strangerContext, detail!.Photos[0].Id);

        Assert.Equal(System.Net.HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(ErrorCodes.NotFound, error?.Code);
    }

    [Fact]
    public async Task Admin_can_presign_private_photo_on_pending_report()
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

        var (_, detail) = await context.GetReportAsync(created.Id);
        Assert.NotNull(detail);

        var (loginResponse, adminSession) = await context.Auth.LoginAsync("01011111111", "AdminPass123");
        Assert.Equal(System.Net.HttpStatusCode.OK, loginResponse.StatusCode);
        Assert.NotNull(adminSession);

        context.Client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", adminSession.AccessToken);

        var (response, body) = await context.GetPhotoUrlAsync(detail.Photos[0].Id);

        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body);
        Assert.StartsWith("https://fake.local/", body.Url, StringComparison.Ordinal);
    }

    private static async Task<(HttpResponseMessage Response, ApiError? Error)> GetPhotoUrlWithErrorAsync(
        ReportTestContext context,
        Guid photoId)
    {
        var (response, _) = await context.GetPhotoUrlAsync(photoId);
        var error = await context.ReadErrorAsync(response);
        return (response, error);
    }
}
