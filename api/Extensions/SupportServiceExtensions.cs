using Amanah.Api.Services.Support;

namespace Amanah.Api.Extensions;

public static class SupportServiceExtensions
{
    public static IServiceCollection AddSupportServices(this IServiceCollection services)
    {
        services.AddScoped<SupportService>();
        return services;
    }
}
