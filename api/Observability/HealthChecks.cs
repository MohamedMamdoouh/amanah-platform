using System.Security.Claims;
using System.Text.Json;
using Amanah.Api.Auth;
using Amanah.Api.Data;
using Microsoft.EntityFrameworkCore;
using Amanah.Api.Options;
using Amanah.Api.Services.Storage;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace Amanah.Api.Observability;

public sealed class DatabaseHealthCheck(AppDbContext dbContext) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        if (!await dbContext.Database.CanConnectAsync(cancellationToken))
        {
            return HealthCheckResult.Unhealthy("Cannot connect to database.");
        }

        await dbContext.Database.ExecuteSqlRawAsync("SELECT 1", cancellationToken);
        return HealthCheckResult.Healthy();
    }
}

public sealed class StorageHealthCheck(
    IBucketStorage bucketStorage,
    IOptions<BucketOptions> bucketOptions) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        if (!bucketOptions.Value.IsConfigured)
        {
            return HealthCheckResult.Healthy("In-memory storage.");
        }

        return await bucketStorage.PingAsync(cancellationToken)
            ? HealthCheckResult.Healthy()
            : HealthCheckResult.Unhealthy("Object storage is unreachable.");
    }
}

public static class HealthCheckResponseWriter
{
    public static Task WriteJson(HttpContext context, HealthReport report)
    {
        context.Response.ContentType = "application/json";

        var checks = report.Entries.ToDictionary(
            entry => entry.Key,
            entry => entry.Value.Status.ToString(),
            StringComparer.Ordinal);

        var payload = new
        {
            status = report.Status.ToString(),
            checks,
        };

        return context.Response.WriteAsync(JsonSerializer.Serialize(payload));
    }
}

public static class ObservabilityUserContext
{
    public static string? GetUserId(ClaimsPrincipal? user) =>
        user?.FindFirstValue(AuthClaimTypes.Sub)
        ?? user?.FindFirstValue(ClaimTypes.NameIdentifier);
}
