using Amanah.Api.Models;

namespace Amanah.Api.Middleware;

public sealed class ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
{
    private readonly RequestDelegate _next = next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger = logger;

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (ApiException ex)
        {
            await WriteErrorAsync(context, ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception for {Method} {Path}", context.Request.Method, context.Request.Path);
            await WriteErrorAsync(
                context,
                new ApiException(ErrorCodes.InternalError, "An unexpected error occurred.", StatusCodes.Status500InternalServerError));
        }
    }

    private static async Task WriteErrorAsync(HttpContext context, ApiException ex)
    {
        if (context.Response.HasStarted)
        {
            throw ex;
        }

        context.Response.StatusCode = ex.StatusCode;
        context.Response.ContentType = "application/json";

        if (ex.RetryAfterSeconds is int retryAfter)
        {
            context.Response.Headers.RetryAfter = retryAfter.ToString();
        }

        await context.Response.WriteAsJsonAsync(ex.ToApiError(), ApiJson.SerializerOptions);
    }
}
