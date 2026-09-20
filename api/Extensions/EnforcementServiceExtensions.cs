using Amanah.Api.Services.Enforcement;

namespace Amanah.Api.Extensions;

public static class EnforcementServiceExtensions
{
    public static IServiceCollection AddEnforcementServices(this IServiceCollection services)
    {
        services.AddScoped<ApprovedClaimCancellation>();
        services.AddScoped<AdminTakedownService>();

        return services;
    }
}
