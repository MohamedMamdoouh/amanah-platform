using Amanah.Api.Services.Moderation;
using Amanah.Api.Services.Notifications;

namespace Amanah.Api.Extensions;

public static class ModerationServiceExtensions
{
    public static IServiceCollection AddModerationServices(this IServiceCollection services)
    {
        services.AddScoped<ModerationService>();
        services.AddScoped<NotificationService>();

        return services;
    }
}
