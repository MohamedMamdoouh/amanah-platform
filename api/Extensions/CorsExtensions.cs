using Amanah.Api.Options;

namespace Amanah.Api.Extensions;

public static class CorsExtensions
{
    public static IServiceCollection AddApiCors(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        services.AddOptions<CorsOptions>()
            .Bind(configuration.GetSection(CorsOptions.SectionName))
            .Validate(
                options => environment.IsDevelopment() || options.AllowedOrigins.Length > 0,
                $"{CorsOptions.SectionName}:AllowedOrigins must contain at least one origin outside Development.")
            .ValidateOnStart();

        services.AddCors();
        services.ConfigureOptions<ConfigureDefaultCorsPolicy>();

        return services;
    }
}