using Amanah.Api.Observability;

namespace Amanah.Api.Extensions;

public static class ObservabilityExtensions
{
    public static IHostApplicationBuilder ConfigureObservabilityLogging(
        this IHostApplicationBuilder builder)
    {
        if (builder.Environment.IsProduction())
        {
            builder.Logging.ClearProviders();
            builder.Logging.AddJsonConsole(options =>
            {
                options.IncludeScopes = true;
                options.TimestampFormat = "O";
                options.JsonWriterOptions = new System.Text.Json.JsonWriterOptions
                {
                    Indented = false,
                };
            });
        }

        return builder;
    }

    public static IServiceCollection AddObservability(this IServiceCollection services)
    {
        services.AddSingleton<AppMetrics>();

        services.AddHealthChecks()
            .AddCheck<DatabaseHealthCheck>("database")
            .AddCheck<StorageHealthCheck>("storage");

        return services;
    }

    public static WebApplication MapObservabilityEndpoints(this WebApplication app)
    {
        app.MapGet("/health", () => Results.Json(new { status = "Healthy" }))
            .AllowAnonymous()
            .ExcludeFromDescription();

        app.MapHealthChecks("/health/ready", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
        {
            ResponseWriter = HealthCheckResponseWriter.WriteJson,
        }).AllowAnonymous();

        return app;
    }
}
