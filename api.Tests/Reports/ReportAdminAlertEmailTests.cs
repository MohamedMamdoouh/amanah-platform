using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Amanah.Api.Data.Entities;
using Amanah.Api.Services.External;
using Amanah.Api.Tests.Infrastructure;
using Amanah.Contracts.Requests.Admin;
using Amanah.Contracts.Responses.Reports;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.EntityFrameworkCore;

namespace Amanah.Api.Tests.Reports;

public class ReportAdminAlertEmailTests(ApiWebApplicationFactory factory) : IClassFixture<ApiWebApplicationFactory>
{
    [Fact]
    public async Task Create_report_enqueues_outbox_and_sends_exactly_one_admin_alert_email()
    {
        ResetAlertSender(factory);
        await using var context = await ReportTestContext.CreateAsync(factory);

        var (_, body) = await context.SubmitReportAsync(TestReportHelpers.BuildValidLostRequest());
        Assert.NotNull(body);

        await AdminAlertEmailOutboxTestHelpers.DrainPendingAsync(factory);

        var alert = Assert.Single(factory.AdminAlertEmailSender.SentAlerts);
        Assert.Equal(body.Id, alert.ReportId);
        Assert.Equal("lost", alert.ReportType);
        Assert.Equal("phones", alert.CategoryCode);
    }

    [Fact]
    public async Task Resubmit_enqueues_outbox_and_sends_admin_alert_email_again()
    {
        ResetAlertSender(factory);
        await using var context = await ReportTestContext.CreateAsync(factory);
        var (_, created) = await context.SubmitReportAsync(TestReportHelpers.BuildValidLostRequest());
        Assert.NotNull(created);

        await AdminAlertEmailOutboxTestHelpers.DrainPendingAsync(factory);
        factory.AdminAlertEmailSender.SentAlerts.Clear();

        await RejectAsAdminAsync(context, created.Id);

        var resubmitResponse = await context.ResubmitReportAsync(created.Id);
        Assert.Equal(HttpStatusCode.NoContent, resubmitResponse.StatusCode);

        await AdminAlertEmailOutboxTestHelpers.DrainPendingAsync(factory);

        var alert = Assert.Single(factory.AdminAlertEmailSender.SentAlerts);
        Assert.Equal(created.Id, alert.ReportId);
        Assert.Equal("lost", alert.ReportType);
    }

    [Fact]
    public async Task Create_report_succeeds_when_admin_alert_email_provider_fails()
    {
        ResetAlertSender(factory);
        factory.AdminAlertEmailSender.ShouldThrow = true;
        await using var context = await ReportTestContext.CreateAsync(factory);

        var (response, body) = await context.SubmitReportAsync(TestReportHelpers.BuildValidLostRequest());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body);

        await AdminAlertEmailOutboxTestHelpers.DrainPendingAsync(factory);

        Assert.Empty(factory.AdminAlertEmailSender.SentAlerts);
        await AdminAlertEmailOutboxTestHelpers.WaitForOutboxStatusAsync(
            factory,
            AdminAlertEmailOutboxStatus.Failed);
    }

    [Fact]
    public async Task Transient_resend_failure_keeps_outbox_pending_for_retry()
    {
        ResetAlertSender(factory);
        factory.AdminAlertEmailSender.FailureStatusCode = 503;
        await using var context = await ReportTestContext.CreateAsync(factory);

        var (_, body) = await context.SubmitReportAsync(TestReportHelpers.BuildValidLostRequest());
        Assert.NotNull(body);

        await AdminAlertEmailOutboxTestHelpers.DrainPendingAsync(factory);

        Assert.Empty(factory.AdminAlertEmailSender.SentAlerts);
        var outboxMessage = await context.DbContext.AdminAlertEmailOutboxMessages.SingleAsync();
        Assert.Equal(AdminAlertEmailOutboxStatus.Pending, outboxMessage.Status);
        Assert.Equal(1, outboxMessage.AttemptCount);
        Assert.Contains("503", outboxMessage.LastError, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Permanent_resend_failure_marks_outbox_failed()
    {
        ResetAlertSender(factory);
        factory.AdminAlertEmailSender.FailureStatusCode = 401;
        await using var context = await ReportTestContext.CreateAsync(factory);

        var (_, body) = await context.SubmitReportAsync(TestReportHelpers.BuildValidLostRequest());
        Assert.NotNull(body);

        await AdminAlertEmailOutboxTestHelpers.DrainPendingAsync(factory);

        Assert.Empty(factory.AdminAlertEmailSender.SentAlerts);
        await AdminAlertEmailOutboxTestHelpers.WaitForOutboxStatusAsync(
            factory,
            AdminAlertEmailOutboxStatus.Failed);
    }

    [Fact]
    public async Task Null_admin_alert_sender_does_not_enqueue_when_email_api_key_unset()
    {
        await using var factoryWithoutEmail = new ApiWebApplicationFactoryWithoutEmail();
        await factoryWithoutEmail.InitializeAsync();
        await using var context = await ReportTestContext.CreateAsync(factoryWithoutEmail);

        var (_, body) = await context.SubmitReportAsync(TestReportHelpers.BuildValidLostRequest());
        Assert.NotNull(body);

        Assert.Empty(context.DbContext.AdminAlertEmailOutboxMessages);
        Assert.Empty(factoryWithoutEmail.AdminAlertEmailSender.SentAlerts);
        await factoryWithoutEmail.DisposeAsync();
    }

    private static void ResetAlertSender(ApiWebApplicationFactory factory)
    {
        factory.AdminAlertEmailSender.SentAlerts.Clear();
        factory.AdminAlertEmailSender.ShouldThrow = false;
        factory.AdminAlertEmailSender.FailureStatusCode = null;
    }

    private static async Task RejectAsAdminAsync(ReportTestContext context, Guid reportId)
    {
        var (loginResponse, adminSession) = await context.Auth.LoginAsync("01011111111", "AdminPass123");
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);
        Assert.NotNull(adminSession);

        context.Client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", adminSession.AccessToken);

        var rejectResponse = await context.Client.PostAsJsonAsync(
            $"/api/v1/admin/moderation/reports/{reportId}/reject",
            new RejectReportRequest
            {
                ReasonCode = "rejection.insufficient_description",
            });
        Assert.Equal(HttpStatusCode.NoContent, rejectResponse.StatusCode);

        context.Client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", context.Session.AccessToken);
    }
}

public sealed class ApiWebApplicationFactoryWithoutEmail : ApiWebApplicationFactory
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Email:ApiKey"] = "",
                ["Email:FromAddress"] = "",
                ["Email:AdminAlertTo"] = "",
            });
        });

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IAdminAlertEmailSender>();
            services.AddSingleton<IAdminAlertEmailSender, NullAdminAlertEmailSender>();
        });
    }
}
