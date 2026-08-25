using Amanah.Api.Options;
using Amanah.Api.Services.Infrastructure;

namespace Amanah.Api.Extensions;

public static class CachingExtensions
{
    public static IServiceCollection AddApiCaching(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<CacheOptions>()
            .Bind(configuration.GetSection(CacheOptions.SectionName));

        services.AddHybridCache();
        services.AddSingleton<ICacheService, CacheService>();

        return services;
    }
}
