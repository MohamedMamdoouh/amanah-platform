using System.Net;
using System.Net.Http.Json;
using Amanah.Api.Data.Entities;
using Amanah.Api.Services.Lifecycle;
using Amanah.Api.Services.Storage;
using Amanah.Api.Services.Uploads;
using Amanah.Api.Tests.Auth;
using Amanah.Api.Tests.Chats;
using Amanah.Api.Tests.Claims;
using Amanah.Api.Tests.Infrastructure;
using Amanah.Api.Tests.Reports;
using Amanah.Api.Tests.Resolution;
using Amanah.Api.Tests.Uploads;
using Amanah.Contracts.Errors;
using Amanah.Contracts.Requests.Claims;
using Amanah.Contracts.Responses.Account;
using Amanah.Contracts.Responses.Auth;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Amanah.Api.Tests.Lifecycle;

public class AccountDeactivationTests(ApiWebApplicationFactory factory) : IClassFixture<ApiWebApplicationFactory>
{
    [Fact]
    public async Task DeactivationStatus_lists_blocker_when_reporter_has_claim_in_progress()
    {
        await using var context = await ReportTestContext.CreateAsync(factory);
        await ResolutionTestHelpers.CreateApprovedClaimScenarioAsync(context);

        var (response, status) = await GetDeactivationStatusAsync(context);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(status);
        Assert.False(status.CanDeactivate);
        Assert.Equal(ErrorCodes.AccountBlockerClaimInProgress, status.Blockers.Single());
    }

    [Fact]
    public async Task DeactivateAccount_returns_conflict_when_reporter_has_claim_in_progress()
    {
        await using var context = await ReportTestContext.CreateAsync(factory);
        await ResolutionTestHelpers.CreateApprovedClaimScenarioAsync(context);

        var response = await DeactivateAccountAsync(context);
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);

        var error = await HttpTestHelpers.ReadErrorAsync(response);
        Assert.Equal(ErrorCodes.AccountDeactivationBlocked, error?.Code);
        Assert.Contains(
            ErrorCodes.AccountBlockerClaimInProgress,
            error?.Errors?["blockers"] ?? []);
    }

    [Fact]
    public async Task DeactivationStatus_lists_blocker_when_claimant_has_approved_claim()
    {
        await using var context = await ReportTestContext.CreateAsync(factory);
        var scenario = await ResolutionTestHelpers.CreateApprovedClaimScenarioAsync(context);

        ClaimTestHelpers.Authenticate(context.Client, scenario.ClaimantSession.AccessToken);

        var (response, status) = await GetDeactivationStatusAsync(context);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(status);
        Assert.False(status.CanDeactivate);
        Assert.Equal(ErrorCodes.AccountBlockerApprovedClaim, status.Blockers.Single());
    }

    [Fact]
    public async Task DeactivateAccount_returns_conflict_when_claimant_has_approved_claim()
    {
        await using var context = await ReportTestContext.CreateAsync(factory);
        var scenario = await ResolutionTestHelpers.CreateApprovedClaimScenarioAsync(context);

        ClaimTestHelpers.Authenticate(context.Client, scenario.ClaimantSession.AccessToken);

        var response = await DeactivateAccountAsync(context);
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);

        var error = await HttpTestHelpers.ReadErrorAsync(response);
        Assert.Equal(ErrorCodes.AccountDeactivationBlocked, error?.Code);
        Assert.Contains(
            ErrorCodes.AccountBlockerApprovedClaim,
            error?.Errors?["blockers"] ?? []);
    }

    [Fact]
    public async Task DeactivateAccount_withdraws_reports_and_pending_claims_keeps_messages_and_pii()
    {
        await using var context = await ReportTestContext.CreateAsync(factory);
        var reporterId = context.Session.User.Id;
        var publishedReportId = await ClaimTestHelpers.PublishLostReportAsync(context);

        var (_, pendingReport) = await context.SubmitReportAsync(TestReportHelpers.BuildValidLostRequest());
        Assert.NotNull(pendingReport);

        var (otherReportId, _) = await CreateOtherReporterPublishedReportAsync(context);
        var pendingClaimId = await ClaimTestHelpers.SeedPendingClaimAsync(
            context,
            otherReportId,
            reporterId);

        var scenario = await ResolutionTestHelpers.CreateApprovedClaimScenarioAsync(context);
        var chatThreadId = await ChatTestHelpers.GetThreadIdAsync(context.Client, scenario.ClaimId);

        ClaimTestHelpers.AuthenticateReporter(context.Client, context);
        var (sendResponse, sentMessage) = await ChatTestHelpers.SendMessageAsync(
            context.Client,
            chatThreadId,
            "Please confirm pickup");
        Assert.Equal(HttpStatusCode.Created, sendResponse.StatusCode);
        Assert.NotNull(sentMessage);

        var cancelResponse = await ResolutionTestHelpers.CancelClaimAsync(context.Client, scenario.ClaimId);
        Assert.Equal(HttpStatusCode.NoContent, cancelResponse.StatusCode);

        var deactivateResponse = await DeactivateAccountAsync(context);
        Assert.Equal(HttpStatusCode.NoContent, deactivateResponse.StatusCode);

        var publishedReport = await context.DbContext.Reports
            .AsNoTracking()
            .SingleAsync(report => report.Id == publishedReportId);
        Assert.Equal(ReportStatus.Withdrawn, publishedReport.Status);
        Assert.Equal(AccountDeactivationService.WithdrawReason, publishedReport.WithdrawalReason);

        var pendingReviewReport = await context.DbContext.Reports
            .AsNoTracking()
            .SingleAsync(report => report.Id == pendingReport.Id);
        Assert.Equal(ReportStatus.Withdrawn, pendingReviewReport.Status);

        var pendingClaim = await context.DbContext.Claims
            .AsNoTracking()
            .SingleAsync(claim => claim.Id == pendingClaimId);
        Assert.Equal(ClaimStatus.Withdrawn, pendingClaim.Status);

        var deactivatedUser = await context.DbContext.Users
            .AsNoTracking()
            .SingleAsync(user => user.Id == reporterId);
        Assert.NotNull(deactivatedUser.DeactivatedAt);
        Assert.False(string.IsNullOrWhiteSpace(deactivatedUser.NormalizedPhone));
        Assert.False(string.IsNullOrWhiteSpace(deactivatedUser.DisplayName));

        var message = await context.DbContext.Messages
            .AsNoTracking()
            .SingleAsync(item => item.Id == sentMessage.Id);
        Assert.Equal(reporterId, message.SenderId);
    }

    [Fact]
    public async Task DeactivateAccount_deletes_pending_claim_photos_from_storage_and_database()
    {
        await using var context = await ReportTestContext.CreateAsync(factory);
        var (otherReportId, _) = await CreateOtherReporterPublishedReportAsync(context);

        var claimant = await ClaimTestHelpers.CreateAndLoginClaimantAsync(context);
        ClaimTestHelpers.Authenticate(context.Client, claimant.AccessToken);

        var (_, submitted) = await ClaimTestHelpers.SubmitClaimAsync(
            context.Client,
            otherReportId,
            new SubmitClaimRequest
            {
                SubmittedAnswer = ClaimTestHelpers.ValidAnswer,
            },
            [TestImageFactory.CreateMinimalJpeg()]);
        Assert.NotNull(submitted);

        var claim = await context.DbContext.Claims
            .AsNoTracking()
            .SingleAsync(item => item.Id == submitted.Id);
        Assert.False(string.IsNullOrWhiteSpace(claim.PhotoStorageKey));

        var storage = Assert.IsType<FakeBucketStorage>(
            factory.Services.GetRequiredService<IBucketStorage>());
        Assert.True(storage.ContainsKey(claim.PhotoStorageKey!));

        var deactivateResponse = await DeactivateAccountAsync(context);
        Assert.Equal(HttpStatusCode.NoContent, deactivateResponse.StatusCode);

        var updatedClaim = await context.DbContext.Claims
            .AsNoTracking()
            .SingleAsync(item => item.Id == submitted.Id);
        Assert.Equal(ClaimStatus.Withdrawn, updatedClaim.Status);
        Assert.Null(updatedClaim.PhotoStorageKey);
        Assert.True(storage.ContainsKey(claim.PhotoStorageKey!));
        await StorageDeletionOutboxTestHelpers.ProcessPendingOutboxAsync(factory);
        Assert.False(storage.ContainsKey(claim.PhotoStorageKey!));
        Assert.False(storage.ContainsKey(
            ClaimPhotoStorageKeys.ThumbnailForOriginal(claim.PhotoStorageKey!)));
    }

    [Fact]
    public async Task Deactivated_user_login_returns_session_with_reactivation_flag()
    {
        await using var context = await ReportTestContext.CreateAsync(factory);
        await DeactivateAccountAsync(context);

        var (_, session) = await context.Auth.LoginAsync("01012345678", TestAuthHelpers.DefaultPassword);
        Assert.NotNull(session);
        Assert.True(session.User.RequiresAccountReactivation);
    }

    [Fact]
    public async Task Deactivated_user_can_refresh_and_fetch_profile_with_reactivation_flag()
    {
        await using var context = await ReportTestContext.CreateAsync(factory);

        await DeactivateAccountAsync(context);

        var (_, session) = await context.Auth.LoginAsync("01012345678", TestAuthHelpers.DefaultPassword);
        Assert.NotNull(session);

        ClaimTestHelpers.Authenticate(context.Client, session.AccessToken);

        var meResponse = await context.Client.GetAsync("/api/v1/auth/me");
        Assert.Equal(HttpStatusCode.OK, meResponse.StatusCode);
        var profile = await meResponse.Content.ReadFromJsonAsync<UserProfileResponse>();
        Assert.NotNull(profile);
        Assert.True(profile.RequiresAccountReactivation);

        var refreshResponse = await context.Client.PostAsync("/api/v1/auth/refresh", null);
        Assert.Equal(HttpStatusCode.OK, refreshResponse.StatusCode);
        var refreshed = await refreshResponse.Content.ReadFromJsonAsync<AuthSessionResponse>();
        Assert.NotNull(refreshed);
        Assert.True(refreshed.User.RequiresAccountReactivation);
    }

    [Fact]
    public async Task Deactivated_user_is_blocked_from_protected_endpoints_until_reactivation()
    {
        await using var context = await ReportTestContext.CreateAsync(factory);
        await DeactivateAccountAsync(context);

        var (_, session) = await context.Auth.LoginAsync("01012345678", TestAuthHelpers.DefaultPassword);
        Assert.NotNull(session);
        ClaimTestHelpers.Authenticate(context.Client, session.AccessToken);

        var response = await context.Client.GetAsync("/api/v1/notifications");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

        var error = await HttpTestHelpers.ReadErrorAsync(response);
        Assert.Equal(ErrorCodes.AccountReactivationRequired, error?.Code);
    }

    [Fact]
    public async Task ReactivateAccount_clears_flag_and_keeps_withdrawn_reports()
    {
        await using var context = await ReportTestContext.CreateAsync(factory);
        var reporterId = context.Session.User.Id;
        var publishedReportId = await ClaimTestHelpers.PublishLostReportAsync(context);
        await DeactivateAccountAsync(context);

        var (_, session) = await context.Auth.LoginAsync("01012345678", TestAuthHelpers.DefaultPassword);
        Assert.NotNull(session);
        ClaimTestHelpers.Authenticate(context.Client, session.AccessToken);

        var reactivateResponse = await ReactivateAccountAsync(context);
        Assert.Equal(HttpStatusCode.NoContent, reactivateResponse.StatusCode);

        var user = await context.DbContext.Users
            .AsNoTracking()
            .SingleAsync(item => item.Id == reporterId);
        Assert.Null(user.DeactivatedAt);

        var meResponse = await context.Client.GetAsync("/api/v1/auth/me");
        Assert.Equal(HttpStatusCode.OK, meResponse.StatusCode);
        var profile = await meResponse.Content.ReadFromJsonAsync<UserProfileResponse>();
        Assert.NotNull(profile);
        Assert.False(profile.RequiresAccountReactivation);

        var report = await context.DbContext.Reports
            .AsNoTracking()
            .SingleAsync(item => item.Id == publishedReportId);
        Assert.Equal(ReportStatus.Withdrawn, report.Status);
    }

    private static Task<(HttpResponseMessage Response, AccountDeactivationStatusResponse? Body)> GetDeactivationStatusAsync(
        ReportTestContext context) =>
        GetDeactivationStatusAsync(context.Client);

    private static async Task<(HttpResponseMessage Response, AccountDeactivationStatusResponse? Body)>
        GetDeactivationStatusAsync(HttpClient client)
    {
        var response = await client.GetAsync("/api/v1/account/deactivation-status");
        AccountDeactivationStatusResponse? body = response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<AccountDeactivationStatusResponse>()
            : null;

        return (response, body);
    }

    private static Task<HttpResponseMessage> DeactivateAccountAsync(ReportTestContext context) =>
        context.Client.PostAsync("/api/v1/account/deactivate", null);

    private static Task<HttpResponseMessage> ReactivateAccountAsync(ReportTestContext context) =>
        context.Client.PostAsync("/api/v1/account/reactivate", null);

    private static async Task<(Guid ReportId, Guid ReporterId)> CreateOtherReporterPublishedReportAsync(
        ReportTestContext context)
    {
        var loginPhone = $"010{Random.Shared.Next(10000000, 99999999)}";
        var normalizedPhone = $"+20{loginPhone[1..]}";
        var otherReporter = TestAuthHelpers.CreateUser(
            context.Auth.PasswordHasher,
            normalizedPhone,
            "Other Reporter");

        context.DbContext.Users.Add(otherReporter);
        await context.DbContext.SaveChangesAsync();

        var originalAuthorization = context.Client.DefaultRequestHeaders.Authorization;
        var (_, otherSession) = await context.Auth.LoginAsync(loginPhone, TestAuthHelpers.DefaultPassword);
        Assert.NotNull(otherSession);

        ClaimTestHelpers.Authenticate(context.Client, otherSession.AccessToken);
        var reportId = await ClaimTestHelpers.PublishLostReportAsync(context);
        context.Client.DefaultRequestHeaders.Authorization = originalAuthorization;

        return (reportId, otherReporter.Id);
    }
}
