using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Amanah.Api.Tests.Infrastructure;
using Amanah.Api.Tests.Reports;
using Amanah.Contracts.Responses.Notifications;

namespace Amanah.Api.Tests.Notifications;

public class NotificationTests(ApiWebApplicationFactory factory) : IClassFixture<ApiWebApplicationFactory>
{
    [Fact]
    public async Task Approve_creates_unread_notification_for_reporter()
    {
        await using var context = await ReportTestContext.CreateAsync(factory);
        var (_, created) = await context.SubmitReportAsync(TestReportHelpers.BuildValidLostRequest());
        Assert.NotNull(created);

        await ApproveAsAdminAsync(context, created.Id);

        var unreadResponse = await context.Client.GetAsync("/api/v1/notifications/unread-count");
        var unread = await unreadResponse.Content.ReadFromJsonAsync<NotificationUnreadCountResponse>();

        Assert.Equal(HttpStatusCode.OK, unreadResponse.StatusCode);
        Assert.NotNull(unread);
        Assert.Equal(1, unread.Count);
    }

    [Fact]
    public async Task Mark_read_decrements_unread_count()
    {
        await using var context = await ReportTestContext.CreateAsync(factory);
        var (_, created) = await context.SubmitReportAsync(TestReportHelpers.BuildValidLostRequest());
        Assert.NotNull(created);

        await ApproveAsAdminAsync(context, created.Id);

        var listResponse = await context.Client.GetAsync("/api/v1/notifications");
        var list = await listResponse.Content.ReadFromJsonAsync<NotificationListResponse>();
        Assert.NotNull(list);
        Assert.Single(list.Items);

        var markReadResponse = await context.Client.PatchAsync(
            $"/api/v1/notifications/{list.Items[0].Id}/read",
            null);
        Assert.Equal(HttpStatusCode.NoContent, markReadResponse.StatusCode);

        var unreadResponse = await context.Client.GetAsync("/api/v1/notifications/unread-count");
        var unread = await unreadResponse.Content.ReadFromJsonAsync<NotificationUnreadCountResponse>();

        Assert.NotNull(unread);
        Assert.Equal(0, unread.Count);
        Assert.True(list.Items[0].Payload.DeepLink.Contains(created.Id.ToString(), StringComparison.Ordinal));
    }

    [Fact]
    public async Task Mark_all_read_clears_unread_count()
    {
        await using var context = await ReportTestContext.CreateAsync(factory);
        var (_, first) = await context.SubmitReportAsync(TestReportHelpers.BuildValidLostRequest());
        var (_, second) = await context.SubmitReportAsync(
            TestReportHelpers.BuildValidLostRequest(title: "Another lost phone"));
        Assert.NotNull(first);
        Assert.NotNull(second);

        await ApproveAsAdminAsync(context, first.Id);
        await ApproveAsAdminAsync(context, second.Id);

        var markAllResponse = await context.Client.PostAsync("/api/v1/notifications/read-all", null);
        Assert.Equal(HttpStatusCode.NoContent, markAllResponse.StatusCode);

        var unreadResponse = await context.Client.GetAsync("/api/v1/notifications/unread-count");
        var unread = await unreadResponse.Content.ReadFromJsonAsync<NotificationUnreadCountResponse>();

        Assert.NotNull(unread);
        Assert.Equal(0, unread.Count);
    }

    private static async Task ApproveAsAdminAsync(ReportTestContext context, Guid reportId)
    {
        var (loginResponse, adminSession) = await context.Auth.LoginAsync("01011111111", "AdminPass123");
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);
        Assert.NotNull(adminSession);

        context.Client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", adminSession.AccessToken);

        var approveResponse = await context.Client.PostAsync(
            $"/api/v1/admin/moderation/reports/{reportId}/approve",
            null);
        Assert.Equal(HttpStatusCode.NoContent, approveResponse.StatusCode);

        context.Client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", context.Session.AccessToken);
    }
}
