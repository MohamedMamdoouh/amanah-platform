using Amanah.Api.Configuration;

namespace Amanah.Api.Extensions;

public static class CorsExtensions
{
    public static IServiceCollection AddApiCors(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var origins = configuration
            .GetSection(CorsOptions.SectionName)
            .Get<CorsOptions>()?.AllowedOrigins ?? [];

        services.AddCors(options =>
        {
            options.AddDefaultPolicy(policy =>
            {
                if (origins.Length > 0)
                    policy.WithOrigins(origins);

                policy.AllowAnyHeader()
                      .AllowAnyMethod();
            });
        });

        return services;
    }
}