using System.Net;
using System.Net.Http.Json;
using Amanah.Api.Data.Entities;
using Amanah.Api.Services.Lifecycle;
using Amanah.Api.Tests.Claims;
using Amanah.Api.Tests.Chats;
using Amanah.Api.Tests.Infrastructure;
using Amanah.Api.Tests.Reports;
using Amanah.Api.Tests.Resolution;
using Amanah.Api.Utilities.Notifications;
using Amanah.Contracts.Errors;
using Amanah.Contracts.Requests.Admin;
using Amanah.Contracts.Responses.Admin;
using Microsoft.EntityFrameworkCore;

namespace Amanah.Api.Tests.Enforcement;

public class AdminTakedownTests(ApiWebApplicationFactory factory) : IClassFixture<ApiWebApplicationFactory>
{
    [Fact]
    public async Task Takedown_published_report_with_pending_claim_closes_claim_and_notifies_parties()
    {
        await using var context = await ReportTestContext.CreateAsync(factory);
        var reportId = await ClaimTestHelpers.PublishLostReportAsync(context);

        var claimantSession = await ClaimTestHelpers.CreateAndLoginClaimantAsync(context);
        var pendingClaimId = await ClaimTestHelpers.SeedPendingClaimAsync(
            context,
            reportId,
            claimantSession.User.Id);

        await HttpTestHelpers.LoginAsAdminAsync(context);

        var response = await context.Client.PostAsJsonAsync(
            $"/api/v1/admin/reports/{reportId}/takedown",
            new AdminReportTakedownRequest { Note = "policy violation" });

        var body = await response.Content.ReadFromJsonAsync<AdminReportTakedownResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body);
        Assert.Equal(reportId, body.ReportId);
        Assert.Equal("removed_by_admin", body.Status);

        var report = await context.DbContext.Reports
            .AsNoTracking()
            .SingleAsync(item => item.Id == reportId);
        Assert.Equal(ReportStatus.RemovedByAdmin, report.Status);

        var pendingClaim = await context.DbContext.Claims
            .AsNoTracking()
            .SingleAsync(claim => claim.Id == pendingClaimId);
        Assert.Equal(ClaimStatus.Withdrawn, pendingClaim.Status);
        Assert.Equal(ReportLifecycleService.AdminTakedownReason, pendingClaim.DecisionReason);

        var claimantClosedNotification = await context.DbContext.Notifications
            .AsNoTracking()
            .SingleAsync(item =>
                item.UserId == claimantSession.User.Id
                && item.Type == NotificationTypes.ClaimClosedReportUnavailable);
        Assert.Contains(reportId.ToString(), claimantClosedNotification.PayloadJson, StringComparison.Ordinal);

        var reporterTakedownNotification = await context.DbContext.Notifications
            .AsNoTracking()
            .SingleAsync(item =>
                item.UserId == context.Session.User.Id
                && item.Type == NotificationTypes.AdminTakedownAffectingYou);
        Assert.Contains($"/my/reports/{reportId}", reporterTakedownNotification.PayloadJson, StringComparison.Ordinal);

        var moderationAction = await context.DbContext.ModerationActions
            .AsNoTracking()
            .SingleAsync(action =>
                action.ReportId == reportId
                && action.Decision == ModerationDecision.Takedown);
        Assert.Equal(ModerationDecision.Takedown, moderationAction.Decision);
        Assert.Equal("policy violation", moderationAction.Note);
    }

    [Fact]
    public async Task Takedown_claim_in_progress_cancels_claim_makes_chat_read_only_and_notifies_both()
    {
        await using var context = await ReportTestContext.CreateAsync(factory);
        var scenario = await ResolutionTestHelpers.CreateApprovedClaimScenarioAsync(context);
        var threadId = await ChatTestHelpers.GetThreadIdAsync(context.Client, scenario.ClaimId);

        await HttpTestHelpers.LoginAsAdminAsync(context);

        var response = await context.Client.PostAsJsonAsync(
            $"/api/v1/admin/reports/{scenario.ReportId}/takedown",
            new AdminReportTakedownRequest());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var claim = await context.DbContext.Claims
            .AsNoTracking()
            .SingleAsync(item => item.Id == scenario.ClaimId);
        Assert.Equal(ClaimStatus.Cancelled, claim.Status);
        Assert.False(claim.CountsAsFailure);

        var report = await context.DbContext.Reports
            .AsNoTracking()
            .SingleAsync(item => item.Id == scenario.ReportId);
        Assert.Equal(ReportStatus.RemovedByAdmin, report.Status);

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

        var takedownNotifications = await context.DbContext.Notifications
            .AsNoTracking()
            .Where(item => item.Type == NotificationTypes.AdminTakedownAffectingYou)
            .ToListAsync();
        Assert.Equal(2, takedownNotifications.Count);
        Assert.Contains(takedownNotifications, item => item.UserId == context.Session.User.Id);
        Assert.Contains(takedownNotifications, item => item.UserId == scenario.ClaimantSession.User.Id);
    }

    [Fact]
    public async Task Takedown_as_non_admin_returns_forbidden()
    {
        await using var context = await ReportTestContext.CreateAsync(factory);
        var reportId = await ClaimTestHelpers.PublishLostReportAsync(context);

        var response = await context.Client.PostAsJsonAsync(
            $"/api/v1/admin/reports/{reportId}/takedown",
            new AdminReportTakedownRequest());

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Takedown_pending_review_report_returns_conflict()
    {
        await using var context = await ReportTestContext.CreateAsync(factory);
        var (_, created) = await context.SubmitReportAsync(TestReportHelpers.BuildValidLostRequest());
        Assert.NotNull(created);

        await HttpTestHelpers.LoginAsAdminAsync(context);

        var response = await context.Client.PostAsJsonAsync(
            $"/api/v1/admin/reports/{created.Id}/takedown",
            new AdminReportTakedownRequest());

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);

        var error = await HttpTestHelpers.ReadErrorAsync(response);
        Assert.Equal(ErrorCodes.EnforcementReportNotTakedownable, error?.Code);
    }

    [Fact]
    public async Task Takedown_after_approve_uses_claim_in_progress_path_and_cancels_claim()
    {
        await using var context = await ReportTestContext.CreateAsync(factory);
        var reportId = await ClaimTestHelpers.PublishLostReportAsync(context);
        var claimantSession = await ClaimTestHelpers.CreateAndLoginClaimantAsync(context);
        var claimId = await ClaimTestHelpers.SeedPendingClaimAsync(
            context,
            reportId,
            claimantSession.User.Id);

        ClaimTestHelpers.AuthenticateReporter(context.Client, context);
        var approveResponse = await ClaimTestHelpers.ApproveClaimAsync(context.Client, claimId);
        Assert.Equal(HttpStatusCode.NoContent, approveResponse.StatusCode);

        context.DbContext.ChangeTracker.Clear();

        var stalePublishedTransitionRows = await context.DbContext.Reports
            .Where(report =>
                report.Id == reportId
                && report.Status == ReportStatus.Published)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(report => report.Status, ReportStatus.RemovedByAdmin)
                .SetProperty(report => report.UpdatedAt, DateTimeOffset.UtcNow));
        Assert.Equal(0, stalePublishedTransitionRows);

        await HttpTestHelpers.LoginAsAdminAsync(context);
        var takedownResponse = await context.Client.PostAsJsonAsync(
            $"/api/v1/admin/reports/{reportId}/takedown",
            new AdminReportTakedownRequest { Note = "after approve" });

        Assert.Equal(HttpStatusCode.OK, takedownResponse.StatusCode);

        var report = await context.DbContext.Reports
            .AsNoTracking()
            .SingleAsync(item => item.Id == reportId);
        var claim = await context.DbContext.Claims
            .AsNoTracking()
            .SingleAsync(item => item.Id == claimId);

        Assert.Equal(ReportStatus.RemovedByAdmin, report.Status);
        Assert.Equal(ClaimStatus.Cancelled, claim.Status);
        Assert.False(claim.CountsAsFailure);
    }

    [Fact]
    public async Task Concurrent_takedown_and_approve_leaves_consistent_report_and_claim_state()
    {
        await using var context = await ReportTestContext.CreateAsync(factory);
        var reportId = await ClaimTestHelpers.PublishLostReportAsync(context);
        var claimantSession = await ClaimTestHelpers.CreateAndLoginClaimantAsync(context);
        var claimId = await ClaimTestHelpers.SeedPendingClaimAsync(
            context,
            reportId,
            claimantSession.User.Id);

        ClaimTestHelpers.AuthenticateReporter(context.Client, context);

        await using var adminContext = await ReportTestContext.CreateAsync(factory);
        await HttpTestHelpers.LoginAsAdminAsync(adminContext);

        var approveTask = ClaimTestHelpers.ApproveClaimAsync(context.Client, claimId);
        var takedownTask = adminContext.Client.PostAsJsonAsync(
            $"/api/v1/admin/reports/{reportId}/takedown",
            new AdminReportTakedownRequest { Note = "race" });

        await Task.WhenAll(approveTask, takedownTask);

        context.DbContext.ChangeTracker.Clear();

        var report = await context.DbContext.Reports
            .AsNoTracking()
            .SingleAsync(item => item.Id == reportId);
        var claim = await context.DbContext.Claims
            .AsNoTracking()
            .Include(item => item.ChatThread)
            .SingleAsync(item => item.Id == claimId);

        var hybridCorruption =
            report.Status == ReportStatus.RemovedByAdmin
            && claim.Status == ClaimStatus.Approved;
        Assert.False(hybridCorruption);

        if (report.Status == ReportStatus.RemovedByAdmin)
        {
            Assert.NotEqual(ClaimStatus.Approved, claim.Status);
        }
        else
        {
            Assert.Equal(ReportStatus.ClaimInProgress, report.Status);
            Assert.Equal(ClaimStatus.Approved, claim.Status);
            Assert.NotNull(claim.ChatThread);
            Assert.Null(claim.ChatThread!.ReadOnlyAt);
        }
    }
}
