using Amanah.Api.Models.Common;
using Amanah.Api.Models.Errors;
using Amanah.Api.Observability;
using Amanah.Contracts.Errors;
using Microsoft.AspNetCore.Diagnostics;

namespace Amanah.Api.Middleware;

public sealed class ApiExceptionHandler(ILogger<ApiExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        if (httpContext.Response.HasStarted)
        {
            return false;
        }

        var apiException = exception as ApiException
            ?? new ApiException(
                ErrorCodes.InternalError,
                "An unexpected error occurred.",
                StatusCodes.Status500InternalServerError);

        if (apiException == exception)
        {
            using (logger.BeginScope(new Dictionary<string, object?> { ["event"] = "api.error" }))
            {
                logger.LogInformation(
                    "API error {Code} for {Method} {Path}",
                    apiException.Code,
                    httpContext.Request.Method,
                    httpContext.Request.Path);
            }
        }
        else
        {
            var userId = ObservabilityUserContext.GetUserId(httpContext.User);
            var scope = new Dictionary<string, object?> { ["event"] = "api.unhandled_error" };
            if (userId is not null)
            {
                scope["userId"] = userId;
            }

            using (logger.BeginScope(scope))
            {
                logger.LogError(
                    exception,
                    "Unhandled exception for {Method} {Path}",
                    httpContext.Request.Method,
                    httpContext.Request.Path);
            }
        }

        httpContext.Response.StatusCode = apiException.StatusCode;
        httpContext.Response.ContentType = "application/json";

        if (apiException.RetryAfterSeconds is int retryAfter)
        {
            httpContext.Response.Headers.RetryAfter = retryAfter.ToString();
        }

        await httpContext.Response.WriteAsJsonAsync(
            apiException.ToApiError(),
            ApiJson.SerializerOptions,
            cancellationToken);

        return true;
    }
}
