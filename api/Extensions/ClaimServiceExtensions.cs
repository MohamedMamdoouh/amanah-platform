using Amanah.Api.Services.Claims;

namespace Amanah.Api.Extensions;

public static class ClaimServiceExtensions
{
    public static IServiceCollection AddClaimServices(this IServiceCollection services)
    {
        services.AddScoped<ClaimPhotoAttachService>();
        services.AddScoped<ClaimSubmitFormParser>();
        services.AddScoped<IClaimService, ClaimService>();

        return services;
    }
}
