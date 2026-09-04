using System.Threading.RateLimiting;
using Amanah.Api.Auth;
using Amanah.Api.Models.Common;
using Amanah.Api.Observability;
using Amanah.Api.Options;
using Amanah.Contracts.Errors;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace Amanah.Api.Extensions;

public static class RateLimitingExtensions
{
    public static IServiceCollection AddApiRateLimiting(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<RateLimitOptions>()
            .Bind(configuration.GetSection(RateLimitOptions.SectionName));

        services.ConfigureOptions<ConfigureRateLimiterOptions>();
        services.AddRateLimiter();

        return services;
    }
}

public sealed class ConfigureRateLimiterOptions(
    IOptions<RateLimitOptions> rateLimit,
    AppMetrics metrics)
    : IConfigureOptions<RateLimiterOptions>
{
    private readonly RateLimitOptions _rateLimit = rateLimit.Value;

    public void Configure(RateLimiterOptions options)
    {
        options.OnRejected = async (context, cancellationToken) =>
        {
            var endpoint = context.HttpContext.GetEndpoint();
            var policy = endpoint?.Metadata
                .GetMetadata<EnableRateLimitingAttribute>()
                ?.PolicyName;

            metrics.RecordRateLimitRejected(policy);

            var response = context.HttpContext.Response;
            response.StatusCode = StatusCodes.Status429TooManyRequests;

            if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter)
                && retryAfter.TotalSeconds > 0)
            {
                response.Headers.RetryAfter = ((int)Math.Ceiling(retryAfter.TotalSeconds)).ToString();
            }

            await response.WriteAsJsonAsync(
                new ApiError(ErrorCodes.RateLimitExceeded, "Too many requests. Please try again later."),
                ApiJson.SerializerOptions,
                cancellationToken);
        };

        foreach (var (policyName, policy) in _rateLimit.Policies)
        {
            if (string.Equals(policyName, "photo-upload-hourly", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (string.Equals(policyName, "photo-upload", StringComparison.OrdinalIgnoreCase)
                && _rateLimit.Policies.TryGetValue("photo-upload-hourly", out var hourlyPolicy))
            {
                options.AddPolicy(policyName, httpContext =>
                {
                    var partitionKey = ResolvePartitionKey(httpContext, policy.PartitionBy);
                    return RateLimitPartition.Get(partitionKey, _ => RateLimiter.CreateChained(
                        new FixedWindowRateLimiter(new FixedWindowRateLimiterOptions
                        {
                            PermitLimit = policy.PermitLimit,
                            Window = TimeSpan.FromSeconds(policy.WindowSeconds),
                            QueueLimit = 0,
                        }),
                        new FixedWindowRateLimiter(new FixedWindowRateLimiterOptions
                        {
                            PermitLimit = hourlyPolicy.PermitLimit,
                            Window = TimeSpan.FromSeconds(hourlyPolicy.WindowSeconds),
                            QueueLimit = 0,
                        })));
                });
                continue;
            }

            options.AddPolicy(policyName, httpContext =>
                RateLimitPartition.GetFixedWindowLimiter(
                    ResolvePartitionKey(httpContext, policy.PartitionBy),
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = policy.PermitLimit,
                        Window = TimeSpan.FromSeconds(policy.WindowSeconds),
                        QueueLimit = 0,
                    }));
        }
    }

    private static string ResolvePartitionKey(HttpContext httpContext, string partitionBy)
    {
        if (string.Equals(partitionBy, "userId", StringComparison.OrdinalIgnoreCase))
        {
            return httpContext.User.TryGetUserId(out var userId)
                ? userId.ToString()
                : "anonymous";
        }

        return httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    }
}
