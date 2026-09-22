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
using Amanah.Api.Utilities.Notifications;
using Amanah.Contracts.Errors;
using Amanah.Contracts.Requests.Admin;
using Amanah.Contracts.Responses.Admin;
using Microsoft.EntityFrameworkCore;

namespace Amanah.Api.Tests.Enforcement;

public class UserBanTests(ApiWebApplicationFactory factory) : IClassFixture<ApiWebApplicationFactory>
{
    [Fact]
    public async Task Ban_user_withdraws_reports_pending_claims_and_revokes_sessions()
    {
        await using var reporterContext = await ReportTestContext.CreateAsync(factory);
        var ownReportId = await ClaimTestHelpers.PublishLostReportAsync(reporterContext);

        var (otherReportId, _) = await CreateOtherReporterPublishedReportAsync(reporterContext);
        var pendingClaimId = await ClaimTestHelpers.SeedPendingClaimAsync(
            reporterContext,
            otherReportId,
            reporterContext.Session.User.Id);

        await HttpTestHelpers.LoginAsAdminAsync(reporterContext);

        var response = await reporterContext.Client.PostAsJsonAsync(
            $"/api/v1/admin/users/{reporterContext.Session.User.Id}/ban",
            new BanUserRequest { Reason = "Repeated abuse" });

        var body = await response.Content.ReadFromJsonAsync<BanUserResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body);
        Assert.Equal("Repeated abuse", body.BanReason);
        Assert.Equal(reporterContext.Session.User.Id, body.UserId);

        var user = await reporterContext.DbContext.Users
            .AsNoTracking()
            .SingleAsync(item => item.Id == reporterContext.Session.User.Id);
        Assert.True(user.IsBanned);
        Assert.Equal("Repeated abuse", user.BanReason);
        Assert.NotNull(user.BannedAt);

        var ownReport = await reporterContext.DbContext.Reports
            .AsNoTracking()
            .SingleAsync(item => item.Id == ownReportId);
        Assert.Equal(ReportStatus.Withdrawn, ownReport.Status);
        Assert.Equal(ReportLifecycleService.BanCleanupWithdrawReason, ownReport.WithdrawalReason);

        var pendingClaim = await reporterContext.DbContext.Claims
            .AsNoTracking()
            .SingleAsync(item => item.Id == pendingClaimId);
        Assert.Equal(ClaimStatus.Withdrawn, pendingClaim.Status);

        var revokedTokens = await reporterContext.DbContext.RefreshTokens
            .AsNoTracking()
            .Where(token => token.UserId == reporterContext.Session.User.Id)
            .ToListAsync();
        Assert.All(revokedTokens, token => Assert.True(token.IsRevoked));

        reporterContext.Client.DefaultRequestHeaders.Authorization = null;
        var (loginResponse, _) = await reporterContext.Auth.LoginAsync(
            reporterContext.Session.User.Phone,
            TestAuthHelpers.DefaultPassword);
        var loginError = await HttpTestHelpers.ReadErrorAsync(loginResponse);
        Assert.Equal(HttpStatusCode.Forbidden, loginResponse.StatusCode);
        Assert.Equal(ErrorCodes.Banned, loginError?.Code);

        var moderationAction = await reporterContext.DbContext.ModerationActions
            .AsNoTracking()
            .SingleAsync(action =>
                action.Decision == ModerationDecision.Ban
                && action.ReasonCode == reporterContext.Session.User.Id.ToString());
        Assert.Equal("Repeated abuse", moderationAction.Note);
        Assert.Null(moderationAction.ReportId);
    }

    [Fact]
    public async Task Ban_user_in_approved_claim_notifies_counterparty_and_makes_chat_read_only()
    {
        await using var context = await ReportTestContext.CreateAsync(factory);
        var scenario = await ResolutionTestHelpers.CreateApprovedClaimScenarioAsync(context);
        var threadId = await ChatTestHelpers.GetThreadIdAsync(context.Client, scenario.ClaimId);

        await HttpTestHelpers.LoginAsAdminAsync(context);

        var response = await context.Client.PostAsJsonAsync(
            $"/api/v1/admin/users/{scenario.ClaimantSession.User.Id}/ban",
            new BanUserRequest { Reason = "Fraud" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var claim = await context.DbContext.Claims
            .AsNoTracking()
            .SingleAsync(item => item.Id == scenario.ClaimId);
        Assert.Equal(ClaimStatus.Cancelled, claim.Status);

        var report = await context.DbContext.Reports
            .AsNoTracking()
            .SingleAsync(item => item.Id == scenario.ReportId);
        Assert.Equal(ReportStatus.Published, report.Status);

        var counterpartyNotification = await context.DbContext.Notifications
            .AsNoTracking()
            .SingleAsync(item =>
                item.UserId == context.Session.User.Id
                && item.Type == NotificationTypes.ClaimEndedByEnforcement);
        Assert.Contains(scenario.ReportId.ToString(), counterpartyNotification.PayloadJson, StringComparison.Ordinal);

        var chatThread = await context.DbContext.ChatThreads
            .AsNoTracking()
            .SingleAsync(thread => thread.Id == threadId);
        Assert.NotNull(chatThread.ReadOnlyAt);

        ClaimTestHelpers.AuthenticateReporter(context.Client, context);
        var (sendResponse, _) = await ChatTestHelpers.SendMessageAsync(
            context.Client,
            threadId,
            "Should be blocked");
        Assert.Equal(HttpStatusCode.Conflict, sendResponse.StatusCode);

        var sendError = await HttpTestHelpers.ReadErrorAsync(sendResponse);
        Assert.Equal(ErrorCodes.ChatReadOnly, sendError?.Code);
    }

    [Fact]
    public async Task Unban_restores_sign_in_without_restoring_withdrawn_content()
    {
        await using var context = await ReportTestContext.CreateAsync(factory);
        var reportId = await ClaimTestHelpers.PublishLostReportAsync(context);
        var userId = context.Session.User.Id;

        await HttpTestHelpers.LoginAsAdminAsync(context);
        var banResponse = await context.Client.PostAsJsonAsync(
            $"/api/v1/admin/users/{userId}/ban",
            new BanUserRequest { Reason = "Temporary suspension" });
        Assert.Equal(HttpStatusCode.OK, banResponse.StatusCode);

        var unbanResponse = await context.Client.PostAsync(
            $"/api/v1/admin/users/{userId}/unban",
            null);
        var unbanBody = await unbanResponse.Content.ReadFromJsonAsync<UnbanUserResponse>();

        Assert.Equal(HttpStatusCode.OK, unbanResponse.StatusCode);
        Assert.NotNull(unbanBody);
        Assert.Equal(userId, unbanBody.UserId);

        var user = await context.DbContext.Users
            .AsNoTracking()
            .SingleAsync(item => item.Id == userId);
        Assert.False(user.IsBanned);
        Assert.Null(user.BanReason);
        Assert.Null(user.BannedAt);

        var report = await context.DbContext.Reports
            .AsNoTracking()
            .SingleAsync(item => item.Id == reportId);
        Assert.Equal(ReportStatus.Withdrawn, report.Status);

        context.Client.DefaultRequestHeaders.Authorization = null;
        var (loginResponse, session) = await context.Auth.LoginAsync(
            context.Session.User.Phone,
            TestAuthHelpers.DefaultPassword);
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);
        Assert.NotNull(session);

        var unbanAction = await context.DbContext.ModerationActions
            .AsNoTracking()
            .SingleAsync(action =>
                action.Decision == ModerationDecision.Unban
                && action.ReasonCode == userId.ToString());
        Assert.Null(unbanAction.ReportId);
    }

    [Fact]
    public async Task Ban_after_resolution_leaves_resolved_report_and_still_cancels_in_progress_claim()
    {
        await using var context = await ReportTestContext.CreateAsync(factory);
        var resolved = await ResolutionTestHelpers.CreateApprovedClaimScenarioAsync(context);

        ClaimTestHelpers.AuthenticateReporter(context.Client, context);
        var reporterConfirm = await ResolutionTestHelpers.ConfirmResolutionAsync(
            context.Client,
            resolved.ClaimId);
        Assert.Equal(HttpStatusCode.NoContent, reporterConfirm.StatusCode);

        ClaimTestHelpers.Authenticate(context.Client, resolved.ClaimantSession.AccessToken);
        var claimantConfirm = await ResolutionTestHelpers.ConfirmResolutionAsync(
            context.Client,
            resolved.ClaimId);
        Assert.Equal(HttpStatusCode.NoContent, claimantConfirm.StatusCode);

        ClaimTestHelpers.AuthenticateReporter(context.Client, context);
        var inProgress = await ResolutionTestHelpers.CreateApprovedClaimScenarioAsync(context);

        await HttpTestHelpers.LoginAsAdminAsync(context);
        var response = await context.Client.PostAsJsonAsync(
            $"/api/v1/admin/users/{context.Session.User.Id}/ban",
            new BanUserRequest { Reason = "Fraud after a completed return" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        context.DbContext.ChangeTracker.Clear();

        var user = await context.DbContext.Users
            .AsNoTracking()
            .SingleAsync(item => item.Id == context.Session.User.Id);
        Assert.True(user.IsBanned);

        var resolvedReport = await context.DbContext.Reports
            .AsNoTracking()
            .SingleAsync(item => item.Id == resolved.ReportId);
        Assert.Equal(ReportStatus.Resolved, resolvedReport.Status);

        var resolvedClaim = await context.DbContext.Claims
            .AsNoTracking()
            .SingleAsync(item => item.Id == resolved.ClaimId);
        Assert.Equal(ClaimStatus.Approved, resolvedClaim.Status);

        var resolution = await context.DbContext.Resolutions
            .AsNoTracking()
            .SingleAsync(item => item.ReportId == resolved.ReportId);
        Assert.NotNull(resolution.ResolvedAt);

        var inProgressReport = await context.DbContext.Reports
            .AsNoTracking()
            .SingleAsync(item => item.Id == inProgress.ReportId);
        Assert.Equal(ReportStatus.Withdrawn, inProgressReport.Status);
        Assert.Equal(ReportLifecycleService.BanCleanupWithdrawReason, inProgressReport.WithdrawalReason);

        var inProgressClaim = await context.DbContext.Claims
            .AsNoTracking()
            .SingleAsync(item => item.Id == inProgress.ClaimId);
        Assert.Equal(ClaimStatus.Cancelled, inProgressClaim.Status);
    }

    [Fact]
    public async Task Ban_claimant_after_resolution_does_not_republish_the_report()
    {
        await using var context = await ReportTestContext.CreateAsync(factory);
        var resolved = await ResolutionTestHelpers.CreateApprovedClaimScenarioAsync(context);

        ClaimTestHelpers.AuthenticateReporter(context.Client, context);
        var reporterConfirm = await ResolutionTestHelpers.ConfirmResolutionAsync(
            context.Client,
            resolved.ClaimId);
        Assert.Equal(HttpStatusCode.NoContent, reporterConfirm.StatusCode);

        ClaimTestHelpers.Authenticate(context.Client, resolved.ClaimantSession.AccessToken);
        var claimantConfirm = await ResolutionTestHelpers.ConfirmResolutionAsync(
            context.Client,
            resolved.ClaimId);
        Assert.Equal(HttpStatusCode.NoContent, claimantConfirm.StatusCode);

        await HttpTestHelpers.LoginAsAdminAsync(context);
        var response = await context.Client.PostAsJsonAsync(
            $"/api/v1/admin/users/{resolved.ClaimantSession.User.Id}/ban",
            new BanUserRequest { Reason = "Fraud after a completed return" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        context.DbContext.ChangeTracker.Clear();

        var claimant = await context.DbContext.Users
            .AsNoTracking()
            .SingleAsync(item => item.Id == resolved.ClaimantSession.User.Id);
        Assert.True(claimant.IsBanned);

        var report = await context.DbContext.Reports
            .AsNoTracking()
            .SingleAsync(item => item.Id == resolved.ReportId);
        Assert.Equal(ReportStatus.Resolved, report.Status);

        var claim = await context.DbContext.Claims
            .AsNoTracking()
            .SingleAsync(item => item.Id == resolved.ClaimId);
        Assert.Equal(ClaimStatus.Approved, claim.Status);

        var resolution = await context.DbContext.Resolutions
            .AsNoTracking()
            .SingleAsync(item => item.ReportId == resolved.ReportId);
        Assert.NotNull(resolution.ResolvedAt);
    }

    [Fact]
    public async Task Ban_as_non_admin_returns_forbidden()
    {
        await using var context = await ReportTestContext.CreateAsync(factory);

        var response = await context.Client.PostAsJsonAsync(
            $"/api/v1/admin/users/{context.Session.User.Id}/ban",
            new BanUserRequest { Reason = "Nope" });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Ban_already_banned_user_returns_conflict()
    {
        await using var context = await ReportTestContext.CreateAsync(factory);
        var userId = context.Session.User.Id;

        await HttpTestHelpers.LoginAsAdminAsync(context);

        var firstBan = await context.Client.PostAsJsonAsync(
            $"/api/v1/admin/users/{userId}/ban",
            new BanUserRequest { Reason = "First" });
        Assert.Equal(HttpStatusCode.OK, firstBan.StatusCode);

        var secondBan = await context.Client.PostAsJsonAsync(
            $"/api/v1/admin/users/{userId}/ban",
            new BanUserRequest { Reason = "Second" });

        Assert.Equal(HttpStatusCode.Conflict, secondBan.StatusCode);
        var error = await HttpTestHelpers.ReadErrorAsync(secondBan);
        Assert.Equal(ErrorCodes.EnforcementUserAlreadyBanned, error?.Code);
    }

    [Fact]
    public async Task Ban_blocks_active_account_apis_with_pre_ban_access_token()
    {
        await using var context = await ReportTestContext.CreateAsync(factory);
        var preBanAccessToken = context.Session.AccessToken;
        var userId = context.Session.User.Id;

        await HttpTestHelpers.LoginAsAdminAsync(context);
        var banResponse = await context.Client.PostAsJsonAsync(
            $"/api/v1/admin/users/{userId}/ban",
            new BanUserRequest { Reason = "Token must die" });
        Assert.Equal(HttpStatusCode.OK, banResponse.StatusCode);

        // Refresh tokens are revoked on ban, but the access JWT remains cryptographically
        // valid until expiry — ActiveAccount must still reject it.
        ClaimTestHelpers.Authenticate(context.Client, preBanAccessToken);

        var notificationsResponse = await context.Client.GetAsync("/api/v1/notifications");
        Assert.Equal(HttpStatusCode.Forbidden, notificationsResponse.StatusCode);
        var notificationsError = await HttpTestHelpers.ReadErrorAsync(notificationsResponse);
        Assert.Equal(ErrorCodes.Banned, notificationsError?.Code);

        var claimsResponse = await context.Client.GetAsync("/api/v1/claims/mine");
        Assert.Equal(HttpStatusCode.Forbidden, claimsResponse.StatusCode);
        var claimsError = await HttpTestHelpers.ReadErrorAsync(claimsResponse);
        Assert.Equal(ErrorCodes.Banned, claimsError?.Code);
    }

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
