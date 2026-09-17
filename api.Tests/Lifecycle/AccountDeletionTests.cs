using System.Net;
using System.Net.Http.Json;
using Amanah.Api.Data.Entities;
using Amanah.Api.Services.Lifecycle;
using Amanah.Api.Tests.Auth;
using Amanah.Api.Tests.Chats;
using Amanah.Api.Tests.Claims;
using Amanah.Api.Tests.Infrastructure;
using Amanah.Api.Tests.Reports;
using Amanah.Api.Tests.Resolution;
using Amanah.Api.Utilities.Auth;
using Amanah.Contracts.Errors;
using Amanah.Contracts.Responses.Account;
using Amanah.Contracts.Responses.Auth;
using Microsoft.EntityFrameworkCore;

namespace Amanah.Api.Tests.Lifecycle;

public class AccountDeletionTests(ApiWebApplicationFactory factory) : IClassFixture<ApiWebApplicationFactory>
{
    [Fact]
    public async Task DeletionStatus_lists_blocker_when_reporter_has_claim_in_progress()
    {
        await using var context = await ReportTestContext.CreateAsync(factory);
        await ResolutionTestHelpers.CreateApprovedClaimScenarioAsync(context);

        var (response, status) = await GetDeletionStatusAsync(context);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(status);
        Assert.False(status.CanDelete);
        Assert.Contains(
            "You have a report with a claim in progress.",
            status.Blockers.Single(),
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task DeleteAccount_returns_conflict_when_reporter_has_claim_in_progress()
    {
        await using var context = await ReportTestContext.CreateAsync(factory);
        await ResolutionTestHelpers.CreateApprovedClaimScenarioAsync(context);

        var response = await DeleteAccountAsync(context);
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);

        var error = await HttpTestHelpers.ReadErrorAsync(response);
        Assert.Equal(ErrorCodes.AccountDeletionBlocked, error?.Code);
        Assert.Contains(
            error?.Errors?["blockers"] ?? [],
            blocker => blocker.Contains("You have a report with a claim in progress.", StringComparison.Ordinal));
    }

    [Fact]
    public async Task DeletionStatus_lists_blocker_when_claimant_has_approved_claim()
    {
        await using var context = await ReportTestContext.CreateAsync(factory);
        var scenario = await ResolutionTestHelpers.CreateApprovedClaimScenarioAsync(context);

        ClaimTestHelpers.Authenticate(context.Client, scenario.ClaimantSession.AccessToken);

        var (response, status) = await GetDeletionStatusAsync(context);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(status);
        Assert.False(status.CanDelete);
        Assert.Contains(
            "You have an approved claim in progress.",
            status.Blockers.Single(),
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task DeleteAccount_returns_conflict_when_claimant_has_approved_claim()
    {
        await using var context = await ReportTestContext.CreateAsync(factory);
        var scenario = await ResolutionTestHelpers.CreateApprovedClaimScenarioAsync(context);

        ClaimTestHelpers.Authenticate(context.Client, scenario.ClaimantSession.AccessToken);

        var response = await DeleteAccountAsync(context);
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);

        var error = await HttpTestHelpers.ReadErrorAsync(response);
        Assert.Equal(ErrorCodes.AccountDeletionBlocked, error?.Code);
        Assert.Contains(
            error?.Errors?["blockers"] ?? [],
            blocker => blocker.Contains("You have an approved claim in progress.", StringComparison.Ordinal));
    }

    [Fact]
    public async Task DeleteAccount_withdraws_reports_and_pending_claims_anonymizes_messages_and_signs_out()
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

        var deleteResponse = await DeleteAccountAsync(context);
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        var publishedReport = await context.DbContext.Reports
            .AsNoTracking()
            .SingleAsync(report => report.Id == publishedReportId);
        Assert.Equal(ReportStatus.Withdrawn, publishedReport.Status);
        Assert.Equal(AccountDeletionService.WithdrawReason, publishedReport.WithdrawalReason);

        var pendingReviewReport = await context.DbContext.Reports
            .AsNoTracking()
            .SingleAsync(report => report.Id == pendingReport.Id);
        Assert.Equal(ReportStatus.Withdrawn, pendingReviewReport.Status);

        var pendingClaim = await context.DbContext.Claims
            .AsNoTracking()
            .SingleAsync(claim => claim.Id == pendingClaimId);
        Assert.Equal(ClaimStatus.Withdrawn, pendingClaim.Status);

        var deletedUser = await context.DbContext.Users
            .AsNoTracking()
            .SingleAsync(user => user.Id == reporterId);
        Assert.NotNull(deletedUser.DeletionRequestedAt);
        Assert.NotNull(deletedUser.SenderAnonymizedAt);
        Assert.False(string.IsNullOrWhiteSpace(deletedUser.NormalizedPhone));
        Assert.False(string.IsNullOrWhiteSpace(deletedUser.DisplayName));

        var message = await context.DbContext.Messages
            .AsNoTracking()
            .SingleAsync(item => item.Id == sentMessage.Id);
        Assert.Equal(AnonymizedUser.Id, message.SenderId);

        var meResponse = await context.Client.GetAsync("/api/v1/auth/me");
        Assert.Equal(HttpStatusCode.Forbidden, meResponse.StatusCode);

        var meError = await HttpTestHelpers.ReadErrorAsync(meResponse);
        Assert.Equal(ErrorCodes.AccountDeleted, meError?.Code);

        var refreshResponse = await context.Client.PostAsync("/api/v1/auth/refresh", null);
        Assert.Equal(HttpStatusCode.Unauthorized, refreshResponse.StatusCode);
    }

    private static Task<(HttpResponseMessage Response, AccountDeletionStatusResponse? Body)> GetDeletionStatusAsync(
        ReportTestContext context) =>
        GetDeletionStatusAsync(context.Client);

    private static async Task<(HttpResponseMessage Response, AccountDeletionStatusResponse? Body)>
        GetDeletionStatusAsync(HttpClient client)
    {
        var response = await client.GetAsync("/api/v1/account/deletion-status");
        AccountDeletionStatusResponse? body = response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<AccountDeletionStatusResponse>()
            : null;

        return (response, body);
    }

    private static Task<HttpResponseMessage> DeleteAccountAsync(ReportTestContext context) =>
        context.Client.DeleteAsync("/api/v1/account");

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
