using Amanah.Api.Services.Abuse;

namespace Amanah.Api.Extensions;

public static class AbuseServiceExtensions
{
    public static IServiceCollection AddAbuseServices(this IServiceCollection services)
    {
        services.AddScoped<AbuseFlagService>();
        services.AddScoped<AbuseAdminService>();
        services.AddScoped<FlaggedListingInvestigationService>();

        return services;
    }
}
