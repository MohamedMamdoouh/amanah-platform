using System.Text.Json;
using Amanah.Api.Filters;
using Amanah.Api.Middleware;
using Amanah.Api.Options;
using Amanah.Api.Services.Catalog;
using Microsoft.AspNetCore.Mvc;

namespace Amanah.Api.Extensions;

public static class DependencyInjection
{
    public static IServiceCollection AddApi(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        services.AddDatabase(configuration);
        services.AddApiCaching(configuration);
        services.AddCatalogServices();
        services.AddReportServices();
        services.AddModerationServices();
        services.AddBucketStorage();
        services.AddUploadServices();
        services.AddOptions<BucketOptions>()
            .Bind(configuration.GetSection(BucketOptions.SectionName));
        services.AddHttpTimeouts(configuration);
        services.AddAuthServices(configuration, environment);
        services.AddApiValidation();
        services.AddJwtAuthentication(configuration);
        services.AddApiVersioningServices();
        services.AddExceptionHandler<ApiExceptionHandler>();
        services.AddProblemDetails();
        services.AddApiRateLimiting(configuration);
        services.AddApiCors(configuration, environment);
        services.AddForwardedHeaders(configuration);
        services.AddObservability();

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
        app.UseMiddleware<CorrelationIdMiddleware>();
        app.UseMiddleware<RequestLoggingMiddleware>();

        if (app.Environment.IsProduction())
        {
            app.UseDefaultFiles();
            app.UseStaticFiles();
        }

        app.UseRouting();
        app.UseHttpTimeouts();
        app.UseCors();
        app.UseAuthentication();
        app.UseAuthorization();
        app.UseRateLimiter();
        app.MapControllers();
        app.MapObservabilityEndpoints();

        if (app.Environment.IsProduction())
        {
            app.MapFallbackToFile("index.html");
        }

        return app;
    }
}
