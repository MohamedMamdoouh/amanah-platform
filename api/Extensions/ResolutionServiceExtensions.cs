using Amanah.Api.Services.Resolution;

namespace Amanah.Api.Extensions;

public static class ResolutionServiceExtensions
{
    public static IServiceCollection AddResolutionServices(this IServiceCollection services)
    {
        services.AddScoped<ResolutionService>();

        return services;
    }
}
