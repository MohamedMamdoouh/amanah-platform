using System.Collections.Concurrent;
using Amanah.Api.Models;
using Microsoft.Extensions.Options;

namespace Amanah.Api.Middleware;

public sealed class RateLimitMiddleware(RequestDelegate next, IOptions<RateLimitOptions> options)
{
    private readonly RequestDelegate _next = next;
    private readonly RateLimitOptions _options = options.Value;
    private static readonly ConcurrentDictionary<string, WindowState> Windows = new();

    public async Task InvokeAsync(HttpContext context)
    {
        var ip = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var now = DateTimeOffset.UtcNow;
        var windowSeconds = Math.Max(1, _options.WindowSeconds);
        var permitLimit = Math.Max(1, _options.PermitLimit);

        var state = Windows.GetOrAdd(ip, _ => new WindowState(now, 0));
        int? blockedRetryAfter = null;

        lock (state)
        {
            if (now - state.WindowStart >= TimeSpan.FromSeconds(windowSeconds))
            {
                state.WindowStart = now;
                state.Count = 0;
            }

            if (state.Count >= permitLimit)
            {
                var windowEnd = state.WindowStart.AddSeconds(windowSeconds);
                blockedRetryAfter = Math.Max(1, (int)Math.Ceiling((windowEnd - now).TotalSeconds));
            }
            else
            {
                state.Count++;
            }
        }

        if (blockedRetryAfter is int retryAfter)
        {
            context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
            context.Response.Headers.RetryAfter = retryAfter.ToString();
            await context.Response.WriteAsJsonAsync(
                new ApiError(ErrorCodes.RateLimitExceeded, "Too many requests. Please try again later."),
                ApiJson.SerializerOptions);
            return;
        }

        await _next(context);
    }

    private sealed class WindowState(DateTimeOffset windowStart, int count)
    {
        public DateTimeOffset WindowStart { get; set; } = windowStart;

        public int Count { get; set; } = count;
    }
}
