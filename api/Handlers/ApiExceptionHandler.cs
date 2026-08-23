using Amanah.Api.Models;
using Microsoft.AspNetCore.Diagnostics;

namespace Amanah.Api.Handlers;

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

        logger.LogError(
            exception,
            "Unhandled exception for {Method} {Path}",
            httpContext.Request.Method,
            httpContext.Request.Path);

        var apiException = exception as ApiException
            ?? new ApiException(
                ErrorCodes.InternalError,
                "An unexpected error occurred.",
                StatusCodes.Status500InternalServerError);

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
