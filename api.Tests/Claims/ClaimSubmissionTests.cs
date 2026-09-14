using Amanah.Api.Data.Entities;
using Amanah.Api.Services.Claims;
using Amanah.Api.Tests.Browse;
using Amanah.Api.Tests.Infrastructure;
using Amanah.Api.Tests.Reports;
using Amanah.Api.Tests.Uploads;
using Amanah.Api.Utilities.Claims;
using Amanah.Api.Utilities.Reports;
using Amanah.Contracts.Errors;
using Amanah.Contracts.Requests.Claims;
using Microsoft.EntityFrameworkCore;

namespace Amanah.Api.Tests.Claims;

public class ClaimSubmissionTests(ApiWebApplicationFactory factory) : IClassFixture<ApiWebApplicationFactory>
{
    [Fact]
    public async Task Submit_on_published_lost_report_creates_pending_claim()
    {
        await using var context = await CreateContextAsync(factory);
        var reportId = await ClaimTestHelpers.PublishLostReportAsync(context);
        var claimantSession = await ClaimTestHelpers.CreateAndLoginClaimantAsync(context);
        ClaimTestHelpers.Authenticate(context.Client, claimantSession.AccessToken);

        var (response, body) = await ClaimTestHelpers.SubmitClaimAsync(context.Client, reportId);

        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body);
        Assert.Equal("pending", body.Status);

        var claim = await context.DbContext.Claims.SingleAsync(existingClaim => existingClaim.Id == body.Id);

        Assert.Equal(ClaimStatus.Pending, claim.Status);
        Assert.Equal(1, claim.AttemptNumber);
        Assert.False(claim.CountsAsFailure);
        Assert.Equal(ClaimTestHelpers.ValidAnswer, claim.SubmittedAnswer);
    }

    [Fact]
    public async Task Submit_notifies_reporter_of_new_claim()
    {
        await using var context = await CreateContextAsync(factory);
        var reportId = await ClaimTestHelpers.PublishLostReportAsync(context);
        var claimantSession = await ClaimTestHelpers.CreateAndLoginClaimantAsync(context);
        ClaimTestHelpers.Authenticate(context.Client, claimantSession.AccessToken);

        var (response, body) = await ClaimTestHelpers.SubmitClaimAsync(context.Client, reportId);
        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body);

        var notification = await context.DbContext.Notifications
            .AsNoTracking()
            .SingleAsync(item =>
                item.UserId == context.Session.User.Id
                && item.Type == "NewClaimSubmitted");
        Assert.Equal("NewClaimSubmitted", notification.Type);
        Assert.Contains($"/my/reports/{reportId}", notification.PayloadJson, StringComparison.Ordinal);
        Assert.Contains("#claims-section", notification.PayloadJson, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Submit_on_published_found_report_creates_pending_claim()
    {
        await using var context = await CreateContextAsync(factory);
        var reportId = await ClaimTestHelpers.PublishFoundReportAsync(context);
        var claimantSession = await ClaimTestHelpers.CreateAndLoginClaimantAsync(context);
        ClaimTestHelpers.Authenticate(context.Client, claimantSession.AccessToken);

        var (response, body) = await ClaimTestHelpers.SubmitClaimAsync(
            context.Client,
            reportId,
            "Lost blue backpack with school books and a name tag inside.");

        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body);
        Assert.Equal("pending", body.Status);
    }

    [Fact]
    public async Task Submit_rejects_non_published_report_without_creating_claim()
    {
        await using var context = await CreateContextAsync(factory);
        var (response, created) = await context.SubmitReportAsync(TestReportHelpers.BuildValidLostRequest());
        Assert.NotNull(created);

        var claimantSession = await ClaimTestHelpers.CreateAndLoginClaimantAsync(context);
        ClaimTestHelpers.Authenticate(context.Client, claimantSession.AccessToken);

        var (claimResponse, _) = await ClaimTestHelpers.SubmitClaimAsync(context.Client, created.Id);
        var error = await HttpTestHelpers.ReadErrorAsync(claimResponse);

        Assert.Equal(System.Net.HttpStatusCode.Conflict, claimResponse.StatusCode);
        Assert.Equal(ErrorCodes.ClaimInvalidStatus, error?.Code);
        Assert.Equal(0, await ClaimTestHelpers.CountClaimsAsync(context, created.Id, claimantSession.User.Id));
    }

    [Fact]
    public async Task Submit_rejects_own_report()
    {
        await using var context = await CreateContextAsync(factory);
        var reportId = await ClaimTestHelpers.PublishLostReportAsync(context);

        var (response, _) = await ClaimTestHelpers.SubmitClaimAsync(context.Client, reportId);
        var error = await HttpTestHelpers.ReadErrorAsync(response);

        Assert.Equal(System.Net.HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(ErrorCodes.ClaimOwnReport, error?.Code);
        Assert.Equal(0, await ClaimTestHelpers.CountClaimsAsync(context, reportId, context.Session.User.Id));
    }

    [Fact]
    public async Task Submit_rejects_second_pending_claim_on_same_report()
    {
        await using var context = await CreateContextAsync(factory);
        var reportId = await ClaimTestHelpers.PublishLostReportAsync(context);
        var claimantSession = await ClaimTestHelpers.CreateAndLoginClaimantAsync(context);
        ClaimTestHelpers.Authenticate(context.Client, claimantSession.AccessToken);

        var (firstResponse, _) = await ClaimTestHelpers.SubmitClaimAsync(context.Client, reportId);
        Assert.Equal(System.Net.HttpStatusCode.OK, firstResponse.StatusCode);

        var (secondResponse, _) = await ClaimTestHelpers.SubmitClaimAsync(
            context.Client,
            reportId,
            "Another description that should not create a second pending claim.");
        var error = await HttpTestHelpers.ReadErrorAsync(secondResponse);

        Assert.Equal(System.Net.HttpStatusCode.Conflict, secondResponse.StatusCode);
        Assert.Equal(ErrorCodes.ClaimPendingExists, error?.Code);
        Assert.Equal(1, await ClaimTestHelpers.CountClaimsAsync(context, reportId, claimantSession.User.Id));
    }

    [Fact]
    public async Task Submit_rejects_contact_info_in_answer()
    {
        await using var context = await CreateContextAsync(factory);
        var reportId = await ClaimTestHelpers.PublishLostReportAsync(context);
        var claimantSession = await ClaimTestHelpers.CreateAndLoginClaimantAsync(context);
        ClaimTestHelpers.Authenticate(context.Client, claimantSession.AccessToken);

        var (response, _) = await ClaimTestHelpers.SubmitClaimAsync(
            context.Client,
            reportId,
            "Please call me on 01012345678 about this wallet.");
        var error = await HttpTestHelpers.ReadErrorAsync(response);

        Assert.Equal(System.Net.HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(ErrorCodes.ValidationFailed, error?.Code);
        Assert.Contains(
            ContactInfoDetector.ContactInfoMessage,
            error!.Errors![ClaimContentValidator.FieldName]);
        Assert.Equal(0, await ClaimTestHelpers.CountClaimsAsync(context, reportId, claimantSession.User.Id));
    }

    [Fact]
    public async Task Submit_rejects_answer_shorter_than_ten_characters()
    {
        await using var context = await CreateContextAsync(factory);
        var reportId = await ClaimTestHelpers.PublishLostReportAsync(context);
        var claimantSession = await ClaimTestHelpers.CreateAndLoginClaimantAsync(context);
        ClaimTestHelpers.Authenticate(context.Client, claimantSession.AccessToken);

        var (response, _) = await ClaimTestHelpers.SubmitClaimAsync(
            context.Client,
            reportId,
            "Too short");
        var error = await HttpTestHelpers.ReadErrorAsync(response);

        Assert.Equal(System.Net.HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains(ClaimContentValidator.FieldName, error!.Errors!.Keys);
        Assert.Equal(0, await ClaimTestHelpers.CountClaimsAsync(context, reportId, claimantSession.User.Id));
    }

    [Fact]
    public async Task Submit_normalizes_answer_before_persisting()
    {
        await using var context = await CreateContextAsync(factory);
        var reportId = await ClaimTestHelpers.PublishLostReportAsync(context);
        var claimantSession = await ClaimTestHelpers.CreateAndLoginClaimantAsync(context);
        ClaimTestHelpers.Authenticate(context.Client, claimantSession.AccessToken);

        var (response, body) = await ClaimTestHelpers.SubmitClaimAsync(
            context.Client,
            reportId,
            "  Black   leather   wallet   with   red   stripe.  ");
        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body);

        var claim = await context.DbContext.Claims.SingleAsync(existingClaim => existingClaim.Id == body.Id);
        Assert.Equal("Black leather wallet with red stripe.", claim.SubmittedAnswer);
    }

    [Fact]
    public async Task Submit_rejects_after_three_counted_failures()
    {
        await using var context = await CreateContextAsync(factory);
        var reportId = await ClaimTestHelpers.PublishLostReportAsync(context);
        var claimantSession = await ClaimTestHelpers.CreateAndLoginClaimantAsync(context);

        await ClaimTestHelpers.SeedRejectedClaimAsync(
            context,
            reportId,
            claimantSession.User.Id,
            countsAsFailure: true,
            attemptNumber: 1);
        await ClaimTestHelpers.SeedRejectedClaimAsync(
            context,
            reportId,
            claimantSession.User.Id,
            countsAsFailure: true,
            attemptNumber: 2);
        await ClaimTestHelpers.SeedRejectedClaimAsync(
            context,
            reportId,
            claimantSession.User.Id,
            countsAsFailure: true,
            attemptNumber: 3);

        ClaimTestHelpers.Authenticate(context.Client, claimantSession.AccessToken);

        var (response, _) = await ClaimTestHelpers.SubmitClaimAsync(context.Client, reportId);
        var error = await HttpTestHelpers.ReadErrorAsync(response);

        Assert.Equal(System.Net.HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(ErrorCodes.ClaimAttemptLimit, error?.Code);
        Assert.Equal(3, await ClaimTestHelpers.CountClaimsAsync(context, reportId, claimantSession.User.Id));
    }

    [Fact]
    public async Task Submit_allows_retry_after_rejection_when_attempts_remain()
    {
        await using var context = await CreateContextAsync(factory);
        var reportId = await ClaimTestHelpers.PublishLostReportAsync(context);
        var claimantSession = await ClaimTestHelpers.CreateAndLoginClaimantAsync(context);

        await ClaimTestHelpers.SeedRejectedClaimAsync(
            context,
            reportId,
            claimantSession.User.Id,
            countsAsFailure: true,
            attemptNumber: 1);

        ClaimTestHelpers.Authenticate(context.Client, claimantSession.AccessToken);

        var (response, body) = await ClaimTestHelpers.SubmitClaimAsync(context.Client, reportId);

        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body);

        var claim = await context.DbContext.Claims.SingleAsync(existingClaim => existingClaim.Id == body.Id);
        Assert.Equal(2, claim.AttemptNumber);
    }

    [Fact]
    public async Task Submit_rejects_daily_quota_after_five_submissions()
    {
        await using var context = await CreateContextAsync(factory);

        var reportIds = new List<Guid>();
        for (var i = 0; i <= ClaimQuotaService.DailyQuotaLimit; i++)
        {
            reportIds.Add(await BrowseTestHelpers.SeedReportAsync(
                context,
                new BrowseTestHelpers.SeedReportOptions
                {
                    Title = $"Seeded lost report {i} for claim quota testing",
                    Description = "A published lost item used to test the daily claim submission quota.",
                }));
        }

        var claimantSession = await ClaimTestHelpers.CreateAndLoginClaimantAsync(context);
        ClaimTestHelpers.Authenticate(context.Client, claimantSession.AccessToken);

        for (var i = 0; i < ClaimQuotaService.DailyQuotaLimit; i++)
        {
            var (response, _) = await ClaimTestHelpers.SubmitClaimAsync(
                context.Client,
                reportIds[i],
                $"Valid claim description number {i} for quota testing.");
            Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
        }

        var (quotaResponse, _) = await ClaimTestHelpers.SubmitClaimAsync(context.Client, reportIds[^1]);
        var error = await HttpTestHelpers.ReadErrorAsync(quotaResponse);

        Assert.Equal(System.Net.HttpStatusCode.TooManyRequests, quotaResponse.StatusCode);
        Assert.Equal(ErrorCodes.ClaimDailyQuota, error?.Code);
        Assert.True(quotaResponse.Headers.RetryAfter is not null);
    }

    [Fact]
    public async Task Submit_with_photo_attaches_storage_key()
    {
        await using var context = await CreateContextAsync(factory);
        var reportId = await ClaimTestHelpers.PublishLostReportAsync(context);
        var claimantSession = await ClaimTestHelpers.CreateAndLoginClaimantAsync(context);
        ClaimTestHelpers.Authenticate(context.Client, claimantSession.AccessToken);

        var (response, body) = await ClaimTestHelpers.SubmitClaimAsync(
            context.Client,
            reportId,
            new SubmitClaimRequest
            {
                SubmittedAnswer = ClaimTestHelpers.ValidAnswer,
            },
            [TestImageFactory.CreateMinimalJpeg()]);

        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body);

        var claim = await context.DbContext.Claims.SingleAsync(existingClaim => existingClaim.Id == body.Id);
        Assert.NotNull(claim.PhotoStorageKey);
        Assert.StartsWith($"private/claims/{claim.Id:N}/", claim.PhotoStorageKey, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Submit_rejects_invalid_photo_without_creating_claim()
    {
        await using var context = await CreateContextAsync(factory);
        var reportId = await ClaimTestHelpers.PublishLostReportAsync(context);
        var claimantSession = await ClaimTestHelpers.CreateAndLoginClaimantAsync(context);
        ClaimTestHelpers.Authenticate(context.Client, claimantSession.AccessToken);

        var (response, _) = await ClaimTestHelpers.SubmitClaimAsync(
            context.Client,
            reportId,
            new SubmitClaimRequest
            {
                SubmittedAnswer = ClaimTestHelpers.ValidAnswer,
            },
            ["not-an-image"u8.ToArray()]);
        var error = await HttpTestHelpers.ReadErrorAsync(response);

        Assert.Equal(System.Net.HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(ErrorCodes.UploadInvalidFormat, error?.Code);
        Assert.Contains("photo", error!.Errors!.Keys);
        Assert.Equal(0, await ClaimTestHelpers.CountClaimsAsync(context, reportId, claimantSession.User.Id));
    }

    [Fact]
    public async Task Submit_rejects_more_than_one_photo()
    {
        await using var context = await CreateContextAsync(factory);
        var reportId = await ClaimTestHelpers.PublishLostReportAsync(context);
        var claimantSession = await ClaimTestHelpers.CreateAndLoginClaimantAsync(context);
        ClaimTestHelpers.Authenticate(context.Client, claimantSession.AccessToken);

        var (response, _) = await ClaimTestHelpers.SubmitClaimAsync(
            context.Client,
            reportId,
            new SubmitClaimRequest
            {
                SubmittedAnswer = ClaimTestHelpers.ValidAnswer,
            },
            [TestImageFactory.CreateMinimalJpeg(), TestImageFactory.CreateMinimalJpeg()]);
        var error = await HttpTestHelpers.ReadErrorAsync(response);

        Assert.Equal(System.Net.HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(ErrorCodes.ValidationFailed, error?.Code);
        Assert.Contains("photo", error!.Errors!.Keys);
        Assert.Equal(0, await ClaimTestHelpers.CountClaimsAsync(context, reportId, claimantSession.User.Id));
    }

    [Fact]
    public async Task Submit_returns_not_found_for_missing_report()
    {
        await using var context = await CreateContextAsync(factory);
        var claimantSession = await ClaimTestHelpers.CreateAndLoginClaimantAsync(context);
        ClaimTestHelpers.Authenticate(context.Client, claimantSession.AccessToken);

        var (response, _) = await ClaimTestHelpers.SubmitClaimAsync(context.Client, Guid.NewGuid());
        var error = await HttpTestHelpers.ReadErrorAsync(response);

        Assert.Equal(System.Net.HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(ErrorCodes.NotFound, error?.Code);
    }

    private static Task<ReportTestContext> CreateContextAsync(ApiWebApplicationFactory factory) =>
        ReportTestContext.CreateAsync(factory);
}
