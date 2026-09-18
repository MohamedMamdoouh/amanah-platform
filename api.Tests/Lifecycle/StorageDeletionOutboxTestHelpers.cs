using Amanah.Api.Services.Storage;
using Amanah.Api.Tests.Infrastructure;
using Amanah.Api.Tests.Reports;
using Microsoft.Extensions.DependencyInjection;

namespace Amanah.Api.Tests.Lifecycle;

public static class StorageDeletionOutboxTestHelpers
{
    public static async Task ProcessPendingOutboxAsync(IServiceProvider services)
    {
        await using var scope = services.CreateAsyncScope();
        var batchService = scope.ServiceProvider.GetRequiredService<StorageDeletionOutboxBatchService>();

        while (await batchService.ProcessPendingBatchAsync() > 0)
        {
        }
    }

    public static Task ProcessPendingOutboxAsync(ApiWebApplicationFactory factory) =>
        ProcessPendingOutboxAsync(factory.Services);

    public static async Task<HttpResponseMessage> RunProcessJobAsync(ReportTestContext context)
    {
        await HttpTestHelpers.LoginAsAdminAsync(context);
        return await context.Client.PostAsync("/api/v1/admin/test/run-job/StorageDeletionOutboxProcess", null);
    }

    public static async Task<HttpResponseMessage> RunCleanupJobAsync(ReportTestContext context)
    {
        await HttpTestHelpers.LoginAsAdminAsync(context);
        return await context.Client.PostAsync("/api/v1/admin/test/run-job/StorageDeletionOutboxCleanup", null);
    }
}
