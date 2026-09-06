using Amanah.Api.Data;
using Amanah.Api.Data.Entities;
using Amanah.Api.Services.Reports;
using Amanah.Api.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Amanah.Api.Tests.Reports;

internal static class AdminAlertEmailOutboxTestHelpers
{
    public static async Task DrainPendingAsync(ApiWebApplicationFactory factory)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var dispatcher = scope.ServiceProvider.GetRequiredService<AdminAlertEmailOutboxDispatcher>();

        var pendingIds = await dbContext.AdminAlertEmailOutboxMessages
            .Where(message => message.Status == AdminAlertEmailOutboxStatus.Pending)
            .Select(message => message.Id)
            .ToListAsync();

        foreach (var outboxId in pendingIds)
        {
            await dispatcher.DispatchAsync(outboxId);
        }
    }

    public static async Task<AdminAlertEmailOutboxStatus> WaitForOutboxStatusAsync(
        ApiWebApplicationFactory factory,
        AdminAlertEmailOutboxStatus expectedStatus,
        int maxAttempts = 50)
    {
        for (var attempt = 0; attempt < maxAttempts; attempt++)
        {
            await using var scope = factory.Services.CreateAsyncScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var status = await dbContext.AdminAlertEmailOutboxMessages
                .OrderByDescending(message => message.CreatedAt)
                .Select(message => message.Status)
                .FirstOrDefaultAsync();

            if (status == expectedStatus)
            {
                return status;
            }

            await Task.Delay(20);
        }

        await using var finalScope = factory.Services.CreateAsyncScope();
        var finalDbContext = finalScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var actualStatus = await finalDbContext.AdminAlertEmailOutboxMessages
            .OrderByDescending(message => message.CreatedAt)
            .Select(message => message.Status)
            .FirstOrDefaultAsync();

        Assert.Equal(expectedStatus, actualStatus);
        return actualStatus;
    }
}
