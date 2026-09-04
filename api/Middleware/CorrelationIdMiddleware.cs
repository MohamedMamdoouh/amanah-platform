using Amanah.Api.Observability;

namespace Amanah.Api.Middleware;

public sealed class CorrelationIdMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, ILogger<CorrelationIdMiddleware> logger)
    {
        var requestId = context.Request.Headers[ObservabilityContext.RequestId].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(requestId))
        {
            requestId = Guid.NewGuid().ToString("N");
        }

        context.Response.Headers[ObservabilityContext.RequestId] = requestId;
        context.Items[ObservabilityContext.RequestId] = requestId;

        using (logger.BeginScope(new Dictionary<string, object?> { ["requestId"] = requestId }))
        {
            await next(context);
        }
    }
}
