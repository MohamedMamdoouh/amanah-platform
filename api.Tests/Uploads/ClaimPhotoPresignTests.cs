using System.Net.Http.Headers;
using Amanah.Api.Tests.Claims;
using Amanah.Api.Tests.Infrastructure;
using Amanah.Api.Tests.Reports;
using Amanah.Contracts.Errors;
using Amanah.Contracts.Requests.Claims;

namespace Amanah.Api.Tests.Uploads;

public class ClaimPhotoPresignTests(ApiWebApplicationFactory factory) : IClassFixture<ApiWebApplicationFactory>
{
    [Fact]
    public async Task Claimant_can_presign_own_claim_photo()
    {
        await using var reporterContext = await ReportTestContext.CreateAsync(factory);
        var reportId = await ClaimTestHelpers.PublishLostReportAsync(reporterContext);

        var claimantSession = await ClaimTestHelpers.CreateAndLoginClaimantAsync(reporterContext);
        ClaimTestHelpers.Authenticate(reporterContext.Client, claimantSession.AccessToken);

        var (submitResponse, submitBody) = await ClaimTestHelpers.SubmitClaimAsync(
            reporterContext.Client,
            reportId,
            new SubmitClaimRequest
            {
                SubmittedAnswer = ClaimTestHelpers.ValidAnswer,
            },
            [TestImageFactory.CreateMinimalJpeg()]);
        Assert.Equal(System.Net.HttpStatusCode.OK, submitResponse.StatusCode);
        Assert.NotNull(submitBody);

        var (response, body) = await ClaimTestHelpers.GetClaimPhotoUrlAsync(
            reporterContext.Client,
            submitBody.Id);

        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body);
        Assert.StartsWith("https://fake.local/", body.Url, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Reporter_can_presign_claim_photo_on_own_report()
    {
        await using var reporterContext = await ReportTestContext.CreateAsync(factory);
        var reportId = await ClaimTestHelpers.PublishLostReportAsync(reporterContext);

        var claimantSession = await ClaimTestHelpers.CreateAndLoginClaimantAsync(reporterContext);
        ClaimTestHelpers.Authenticate(reporterContext.Client, claimantSession.AccessToken);

        var (_, submitBody) = await ClaimTestHelpers.SubmitClaimAsync(
            reporterContext.Client,
            reportId,
            new SubmitClaimRequest
            {
                SubmittedAnswer = ClaimTestHelpers.ValidAnswer,
            },
            [TestImageFactory.CreateMinimalJpeg()]);
        Assert.NotNull(submitBody);

        ClaimTestHelpers.Authenticate(reporterContext.Client, reporterContext.Session.AccessToken);

        var (response, body) = await ClaimTestHelpers.GetClaimPhotoUrlAsync(
            reporterContext.Client,
            submitBody.Id);

        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body);
        Assert.StartsWith("https://fake.local/", body.Url, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Stranger_cannot_presign_claim_photo()
    {
        await using var reporterContext = await ReportTestContext.CreateAsync(factory);
        var reportId = await ClaimTestHelpers.PublishLostReportAsync(reporterContext);

        var claimantSession = await ClaimTestHelpers.CreateAndLoginClaimantAsync(reporterContext);
        ClaimTestHelpers.Authenticate(reporterContext.Client, claimantSession.AccessToken);

        var (_, submitBody) = await ClaimTestHelpers.SubmitClaimAsync(
            reporterContext.Client,
            reportId,
            new SubmitClaimRequest
            {
                SubmittedAnswer = ClaimTestHelpers.ValidAnswer,
            },
            [TestImageFactory.CreateMinimalJpeg()]);
        Assert.NotNull(submitBody);

        await using var strangerContext = await ReportTestContext.CreateAsync(factory);
        var (response, error) = await GetClaimPhotoUrlWithErrorAsync(strangerContext, submitBody.Id);

        Assert.Equal(System.Net.HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(ErrorCodes.NotFound, error?.Code);
    }

    [Fact]
    public async Task Admin_cannot_presign_claim_photo_before_phase_07()
    {
        await using var reporterContext = await ReportTestContext.CreateAsync(factory);
        var reportId = await ClaimTestHelpers.PublishLostReportAsync(reporterContext);

        var claimantSession = await ClaimTestHelpers.CreateAndLoginClaimantAsync(reporterContext);
        ClaimTestHelpers.Authenticate(reporterContext.Client, claimantSession.AccessToken);

        var (_, submitBody) = await ClaimTestHelpers.SubmitClaimAsync(
            reporterContext.Client,
            reportId,
            new SubmitClaimRequest
            {
                SubmittedAnswer = ClaimTestHelpers.ValidAnswer,
            },
            [TestImageFactory.CreateMinimalJpeg()]);
        Assert.NotNull(submitBody);

        var (loginResponse, adminSession) = await reporterContext.Auth.LoginAsync("01011111111", "AdminPass123");
        Assert.Equal(System.Net.HttpStatusCode.OK, loginResponse.StatusCode);
        Assert.NotNull(adminSession);

        reporterContext.Client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", adminSession.AccessToken);

        var (response, error) = await GetClaimPhotoUrlWithErrorAsync(reporterContext, submitBody.Id);

        Assert.Equal(System.Net.HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(ErrorCodes.Forbidden, error?.Code);
    }

    private static async Task<(HttpResponseMessage Response, ApiError? Error)> GetClaimPhotoUrlWithErrorAsync(
        ReportTestContext context,
        Guid claimId)
    {
        var (response, _) = await ClaimTestHelpers.GetClaimPhotoUrlAsync(context.Client, claimId);
        var error = await context.ReadErrorAsync(response);
        return (response, error);
    }
}
