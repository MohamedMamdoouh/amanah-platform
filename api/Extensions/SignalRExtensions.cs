using System.Text.Json;
using Amanah.Api.Hubs;
using Amanah.Api.Services.Chats;
using Amanah.Contracts.Chats;

namespace Amanah.Api.Extensions;

public static class SignalRExtensions
{
    public static IServiceCollection AddChatSignalR(this IServiceCollection services)
    {
        services.AddSingleton<ChatPresenceTracker>();
        services.AddSignalR()
            .AddJsonProtocol(options =>
            {
                options.PayloadSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
            });

        return services;
    }

    public static WebApplication MapChatHub(this WebApplication app)
    {
        app.MapHub<ChatHub>(ChatHubRoutes.Path);
        return app;
    }
}
