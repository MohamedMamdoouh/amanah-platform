using System.Diagnostics;
using Amanah.Api.Observability;

namespace Amanah.Api.Middleware;

public sealed class RequestLoggingMiddleware(
    RequestDelegate next,
    ILogger<RequestLoggingMiddleware> logger,
    AppMetrics metrics)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var sw = Stopwatch.StartNew();
        await next(context);
        sw.Stop();

        var path = context.Request.Path.Value ?? "/";
        if (IsHealthProbe(path) && context.Response.StatusCode < 400)
        {
            return;
        }

        var userId = ObservabilityUserContext.GetUserId(context.User);
        var scope = new Dictionary<string, object?> { ["event"] = "http.request.completed" };
        if (userId is not null)
        {
            scope["userId"] = userId;
        }

        using (logger.BeginScope(scope))
        {
            logger.LogInformation(
                "HTTP {Method} {Path} -> {StatusCode} ({DurationMs}ms)",
                context.Request.Method,
                path,
                context.Response.StatusCode,
                sw.ElapsedMilliseconds);
        }

        metrics.RecordHttpRequest(
            sw.ElapsedMilliseconds,
            context.Request.Method,
            path,
            context.Response.StatusCode);
    }

    private static bool IsHealthProbe(string path) =>
        path.Equals("/health", StringComparison.OrdinalIgnoreCase)
        || path.Equals("/health/ready", StringComparison.OrdinalIgnoreCase);
}
