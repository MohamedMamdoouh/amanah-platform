using System.Text.Json;
using Amanah.Api.Filters;
using Amanah.Api.Handlers;
using Amanah.Api.Middleware;
using Microsoft.AspNetCore.Mvc;

namespace Amanah.Api.Extensions;

public static class DependencyInjection
{
    public static IServiceCollection AddApi(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddExceptionHandler<ApiExceptionHandler>();
        services.AddProblemDetails();
        services.AddApiRateLimiting(configuration);
        services.AddApiCors(configuration);
        services.AddForwardedHeaders(configuration);

        services.AddControllers(options => options.Filters.Add<ApiValidationFilter>())
            .AddJsonOptions(options =>
            {
                options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
            });

        services.Configure<ApiBehaviorOptions>(options =>
        {
            options.SuppressModelStateInvalidFilter = true;
        });

        return services;
    }

    public static WebApplication UsePipeline(this WebApplication app)
    {
        app.UseExceptionHandler();
        app.UseForwardedHeaders();
        app.UseMiddleware<RequestLoggingMiddleware>();
        app.UseRouting();
        app.UseCors();
        app.UseRateLimiter();
        app.MapControllers();

        return app;
    }
}
