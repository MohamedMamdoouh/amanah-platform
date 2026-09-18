using System.Net;
using System.Net.Http.Json;
using Amanah.Api.Tests.Infrastructure;
using Amanah.Api.Tests.Reports;
using Amanah.Contracts.Errors;
using Amanah.Contracts.Requests.Admin;
using Amanah.Contracts.Responses.Admin;

namespace Amanah.Api.Tests.Admin;

public class AdminAuthorizationTests(ApiWebApplicationFactory factory) : IClassFixture<ApiWebApplicationFactory>
{
    [Fact]
    public async Task Get_moderation_queue_without_auth_returns_unauthorized()
    {
        await using var context = await ReportTestContext.CreateAsync(factory);
        context.Client.DefaultRequestHeaders.Authorization = null;

        var response = await context.Client.GetAsync("/api/v1/admin/moderation/queue");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Get_moderation_queue_as_user_returns_forbidden()
    {
        await using var context = await ReportTestContext.CreateAsync(factory);

        var response = await context.Client.GetAsync("/api/v1/admin/moderation/queue");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Get_moderation_queue_as_admin_returns_queue()
    {
        await using var context = await ReportTestContext.CreateAsync(factory);
        await HttpTestHelpers.LoginAsAdminAsync(context);

        var response = await context.Client.GetAsync("/api/v1/admin/moderation/queue");
        var body = await response.Content.ReadFromJsonAsync<ModerationQueueResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body);
        Assert.Empty(body.Items);
        Assert.Equal(0, body.PendingCount);
    }

    [Fact]
    public async Task Deactivated_admin_is_blocked_from_moderation_until_reactivation()
    {
        await using var context = await ReportTestContext.CreateAsync(factory);
        await HttpTestHelpers.LoginAsAdminAsync(context);

        var deactivateResponse = await context.Client.PostAsync("/api/v1/account/deactivate", null);
        Assert.Equal(HttpStatusCode.NoContent, deactivateResponse.StatusCode);

        await HttpTestHelpers.LoginAsAdminAsync(context);

        var blockedResponse = await context.Client.GetAsync("/api/v1/admin/moderation/queue");
        Assert.Equal(HttpStatusCode.Forbidden, blockedResponse.StatusCode);

        var blockedError = await HttpTestHelpers.ReadErrorAsync(blockedResponse);
        Assert.Equal(ErrorCodes.AccountReactivationRequired, blockedError?.Code);

        var reactivateResponse = await context.Client.PostAsync("/api/v1/account/reactivate", null);
        Assert.Equal(HttpStatusCode.NoContent, reactivateResponse.StatusCode);

        var allowedResponse = await context.Client.GetAsync("/api/v1/admin/moderation/queue");
        Assert.Equal(HttpStatusCode.OK, allowedResponse.StatusCode);
    }

    [Fact]
    public async Task Get_admin_categories_as_admin_returns_category_list()
    {
        await using var context = await ReportTestContext.CreateAsync(factory);
        await HttpTestHelpers.LoginAsAdminAsync(context);

        var response = await context.Client.GetAsync("/api/v1/admin/categories");
        var body = await response.Content.ReadFromJsonAsync<AdminCategoryListResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body);
        Assert.NotEmpty(body.Items);
    }

    [Fact]
    public async Task Reject_report_with_invalid_reason_returns_bad_request()
    {
        await using var context = await ReportTestContext.CreateAsync(factory);
        await HttpTestHelpers.LoginAsAdminAsync(context);

        var response = await context.Client.PostAsJsonAsync(
            $"/api/v1/admin/moderation/reports/{Guid.NewGuid()}/reject",
            new RejectReportRequest
            {
                ReasonCode = "rejection.invalid_reason",
            });

        var error = await HttpTestHelpers.ReadErrorAsync(response);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(ErrorCodes.ValidationFailed, error?.Code);
        Assert.NotNull(error?.Errors);
        Assert.Contains("reasonCode", error.Errors.Keys);
    }

    [Fact]
    public async Task Reject_report_with_valid_reason_as_admin_returns_not_found_for_missing_report()
    {
        await using var context = await ReportTestContext.CreateAsync(factory);
        await HttpTestHelpers.LoginAsAdminAsync(context);

        var response = await context.Client.PostAsJsonAsync(
            $"/api/v1/admin/moderation/reports/{Guid.NewGuid()}/reject",
            new RejectReportRequest
            {
                ReasonCode = "rejection.insufficient_description",
                Note = "Please add more detail about the item.",
            });

        var error = await HttpTestHelpers.ReadErrorAsync(response);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(ErrorCodes.NotFound, error?.Code);
    }
}
