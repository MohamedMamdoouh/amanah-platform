using System.Text.Json;
using Amanah.Api.Filters;
using Amanah.Api.Middleware;
using Microsoft.AspNetCore.Mvc;

namespace Amanah.Api.Extensions;

public static class DependencyInjection
{
    public static IServiceCollection AddApi(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<RateLimitOptions>(
            configuration.GetSection(RateLimitOptions.SectionName));

        services.AddControllers(options => options.Filters.Add<ApiValidationFilter>())
            .AddJsonOptions(options =>
            {
                options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
            });

        services.Configure<ApiBehaviorOptions>(options =>
        {
            options.SuppressModelStateInvalidFilter = true;
        });

        services.AddCors(options =>
        {
            options.AddDefaultPolicy(policy =>
            {
                policy.WithOrigins("http://localhost:4200")
                    .AllowAnyHeader()
                    .AllowAnyMethod();
            });
        });

        return services;
    }

    public static WebApplication UsePipeline(this WebApplication app)
    {
        app.UseMiddleware<ExceptionHandlingMiddleware>();
        app.UseMiddleware<RequestLoggingMiddleware>();
        app.UseCors();
        app.UseMiddleware<RateLimitMiddleware>();
        app.MapControllers();

        return app;
    }
}
