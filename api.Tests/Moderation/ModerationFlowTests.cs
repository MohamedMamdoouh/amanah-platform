using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Amanah.Api.Data.Entities;
using Amanah.Api.Tests.Infrastructure;
using Amanah.Api.Tests.Reports;
using Amanah.Contracts.Errors;
using Amanah.Contracts.Requests.Admin;
using Amanah.Contracts.Responses.Admin;
using Amanah.Contracts.Responses.Notifications;
using Amanah.Contracts.Responses.Reports;
using Microsoft.EntityFrameworkCore;

namespace Amanah.Api.Tests.Moderation;

public class ModerationFlowTests(ApiWebApplicationFactory factory) : IClassFixture<ApiWebApplicationFactory>
{
    [Fact]
    public async Task Approve_sets_published_status_and_notifies_reporter()
    {
        await using var context = await ReportTestContext.CreateAsync(factory);
        var (_, created) = await context.SubmitReportAsync(TestReportHelpers.BuildValidLostRequest());
        Assert.NotNull(created);

        await LoginAsAdminAsync(context);

        var approveResponse = await context.Client.PostAsync(
            $"/api/v1/admin/moderation/reports/{created.Id}/approve",
            null);
        Assert.Equal(HttpStatusCode.NoContent, approveResponse.StatusCode);

        var report = await context.DbContext.Reports
            .AsNoTracking()
            .SingleAsync(item => item.Id == created.Id);
        Assert.Equal(ReportStatus.Published, report.Status);
        Assert.NotNull(report.PublishedAt);

        var moderationAction = await context.DbContext.ModerationActions
            .AsNoTracking()
            .SingleAsync(action => action.ReportId == created.Id);
        Assert.Equal(ModerationDecision.Approve, moderationAction.Decision);

        var notification = await context.DbContext.Notifications
            .AsNoTracking()
            .SingleAsync(item => item.UserId == context.Session.User.Id);
        Assert.Equal("ReportApproved", notification.Type);
        Assert.False(notification.IsRead);
    }

    [Fact]
    public async Task Reject_stores_reason_and_removes_from_queue()
    {
        await using var context = await ReportTestContext.CreateAsync(factory);
        var (_, created) = await context.SubmitReportAsync(TestReportHelpers.BuildValidLostRequest());
        Assert.NotNull(created);

        await LoginAsAdminAsync(context);

        var rejectResponse = await context.Client.PostAsJsonAsync(
            $"/api/v1/admin/moderation/reports/{created.Id}/reject",
            new RejectReportRequest
            {
                ReasonCode = "rejection.insufficient_description",
                Note = "Add more detail about where you lost it.",
            });
        Assert.Equal(HttpStatusCode.NoContent, rejectResponse.StatusCode);

        var report = await context.DbContext.Reports
            .AsNoTracking()
            .SingleAsync(item => item.Id == created.Id);
        Assert.Equal(ReportStatus.Rejected, report.Status);

        var moderationAction = await context.DbContext.ModerationActions
            .AsNoTracking()
            .SingleAsync(action => action.ReportId == created.Id);
        Assert.Equal(ModerationDecision.Reject, moderationAction.Decision);
        Assert.Equal("rejection.insufficient_description", moderationAction.ReasonCode);
        Assert.Equal("Add more detail about where you lost it.", moderationAction.Note);

        var queueResponse = await context.Client.GetAsync("/api/v1/admin/moderation/queue");
        var queue = await queueResponse.Content.ReadFromJsonAsync<ModerationQueueResponse>();
        Assert.NotNull(queue);
        Assert.DoesNotContain(queue.Items, item => item.Id == created.Id);

        var notification = await context.DbContext.Notifications
            .AsNoTracking()
            .SingleAsync(item => item.UserId == context.Session.User.Id);
        Assert.Equal("ReportRejected", notification.Type);
    }

    [Fact]
    public async Task Approve_non_pending_report_returns_conflict()
    {
        await using var context = await ReportTestContext.CreateAsync(factory);
        var (_, created) = await context.SubmitReportAsync(TestReportHelpers.BuildValidLostRequest());
        Assert.NotNull(created);

        await LoginAsAdminAsync(context);

        var firstApprove = await context.Client.PostAsync(
            $"/api/v1/admin/moderation/reports/{created.Id}/approve",
            null);
        Assert.Equal(HttpStatusCode.NoContent, firstApprove.StatusCode);

        var secondApprove = await context.Client.PostAsync(
            $"/api/v1/admin/moderation/reports/{created.Id}/approve",
            null);
        var error = await context.ReadErrorAsync(secondApprove);

        Assert.Equal(HttpStatusCode.Conflict, secondApprove.StatusCode);
        Assert.Equal(ErrorCodes.Conflict, error?.Code);
    }

    [Fact]
    public async Task Admin_moderation_detail_omits_hidden_detail()
    {
        await using var context = await ReportTestContext.CreateAsync(factory);
        var (_, created) = await context.SubmitReportAsync(TestReportHelpers.BuildValidLostRequest());
        Assert.NotNull(created);

        await LoginAsAdminAsync(context);

        var response = await context.Client.GetAsync(
            $"/api/v1/admin/moderation/reports/{created.Id}");
        var body = await response.Content.ReadFromJsonAsync<ReportDetailResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body);
        Assert.Null(body.HiddenDetail);
    }

    [Fact]
    public async Task Moderation_action_survives_report_deletion()
    {
        await using var context = await ReportTestContext.CreateAsync(factory);
        var (_, created) = await context.SubmitReportAsync(TestReportHelpers.BuildValidLostRequest());
        Assert.NotNull(created);

        await LoginAsAdminAsync(context);

        var rejectResponse = await context.Client.PostAsJsonAsync(
            $"/api/v1/admin/moderation/reports/{created.Id}/reject",
            new RejectReportRequest
            {
                ReasonCode = "rejection.wrong_category",
            });
        Assert.Equal(HttpStatusCode.NoContent, rejectResponse.StatusCode);

        var actionId = await context.DbContext.ModerationActions
            .AsNoTracking()
            .Where(action => action.ReportId == created.Id)
            .Select(action => action.Id)
            .SingleAsync();

        await context.DbContext.Reports
            .Where(report => report.Id == created.Id)
            .ExecuteDeleteAsync();

        var survivingAction = await context.DbContext.ModerationActions
            .AsNoTracking()
            .SingleAsync(action => action.Id == actionId);

        Assert.Null(survivingAction.ReportId);
        Assert.Equal("rejection.wrong_category", survivingAction.ReasonCode);
    }

    [Fact]
    public async Task Queue_returns_pending_reports_in_fifo_order()
    {
        await using var context = await ReportTestContext.CreateAsync(factory);
        var (_, first) = await context.SubmitReportAsync(
            TestReportHelpers.BuildValidLostRequest(title: "First queue item"));
        var (_, second) = await context.SubmitReportAsync(
            TestReportHelpers.BuildValidLostRequest(title: "Second queue item"));
        Assert.NotNull(first);
        Assert.NotNull(second);

        await LoginAsAdminAsync(context);

        var response = await context.Client.GetAsync("/api/v1/admin/moderation/queue");
        var queue = await response.Content.ReadFromJsonAsync<ModerationQueueResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(queue);
        Assert.Equal(2, queue.PendingCount);
        Assert.Equal(first.Id, queue.Items[0].Id);
        Assert.Equal(second.Id, queue.Items[1].Id);
    }

    [Fact]
    public async Task Rejected_report_includes_reason_for_reporter()
    {
        await using var context = await ReportTestContext.CreateAsync(factory);
        var (_, created) = await context.SubmitReportAsync(TestReportHelpers.BuildValidLostRequest());
        Assert.NotNull(created);

        await LoginAsAdminAsync(context);
        await context.Client.PostAsJsonAsync(
            $"/api/v1/admin/moderation/reports/{created.Id}/reject",
            new RejectReportRequest
            {
                ReasonCode = "rejection.contact_info",
                Note = "Remove the phone number from the description.",
            });

        context.Client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", context.Session.AccessToken);

        var (response, body) = await context.GetReportAsync(created.Id);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("rejection.contact_info", body?.RejectionReasonCode);
        Assert.Equal("Remove the phone number from the description.", body?.RejectionNote);
    }

    private static async Task LoginAsAdminAsync(ReportTestContext context)
    {
        var (loginResponse, adminSession) = await context.Auth.LoginAsync("01011111111", "AdminPass123");
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);
        Assert.NotNull(adminSession);

        context.Client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", adminSession.AccessToken);
    }
}
