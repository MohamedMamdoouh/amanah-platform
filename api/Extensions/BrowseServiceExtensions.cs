using Amanah.Api.Services.Browse;

namespace Amanah.Api.Extensions;

public static class BrowseServiceExtensions
{
    public static IServiceCollection AddBrowseServices(this IServiceCollection services)
    {
        services.AddScoped<BrowseService>();

        return services;
    }
}
