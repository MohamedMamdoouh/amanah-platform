using System.Net;
using Amanah.Api.Tests.Infrastructure;
using Amanah.Api.Tests.Reports;

namespace Amanah.Api.Tests.Jobs;

public class AdminTestControllerTests(ApiWebApplicationFactory factory) : IClassFixture<ApiWebApplicationFactory>
{
    [Fact]
    public async Task Run_job_without_auth_returns_unauthorized()
    {
        await using var context = await ReportTestContext.CreateAsync(factory);
        context.Client.DefaultRequestHeaders.Authorization = null;

        var response = await context.Client.PostAsync(
            "/api/v1/admin/test/run-job/ListingAutoExpiry",
            null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Run_job_as_user_returns_forbidden()
    {
        await using var context = await ReportTestContext.CreateAsync(factory);

        var response = await context.Client.PostAsync(
            "/api/v1/admin/test/run-job/ListingAutoExpiry",
            null);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Run_job_as_admin_with_unknown_name_returns_not_found()
    {
        await using var context = await ReportTestContext.CreateAsync(factory);
        await HttpTestHelpers.LoginAsAdminAsync(context);

        var response = await context.Client.PostAsync(
            "/api/v1/admin/test/run-job/UnknownJob",
            null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
