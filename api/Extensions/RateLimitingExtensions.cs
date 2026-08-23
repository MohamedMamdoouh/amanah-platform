using System.Threading.RateLimiting;
using Amanah.Api.Configuration;
using Amanah.Api.Models;
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

public sealed class ConfigureRateLimiterOptions(IOptions<RateLimitOptions> rateLimit)
    : IConfigureOptions<RateLimiterOptions>
{
    private readonly RateLimitOptions _rateLimit = rateLimit.Value;

    public void Configure(RateLimiterOptions options)
    {
        options.OnRejected = async (context, cancellationToken) =>
        {
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
            options.AddPolicy(policyName, httpContext =>
                RateLimitPartition.GetFixedWindowLimiter(
                    httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = policy.PermitLimit,
                        Window = TimeSpan.FromSeconds(policy.WindowSeconds),
                        QueueLimit = 0,
                    }));
        }
    }
}
