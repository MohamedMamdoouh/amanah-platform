using Amanah.Api.Services.Chats;

namespace Amanah.Api.Extensions;

public static class ChatServiceExtensions
{
    public static IServiceCollection AddChatServices(this IServiceCollection services)
    {
        services.AddScoped<ChatService>();

        return services;
    }
}
