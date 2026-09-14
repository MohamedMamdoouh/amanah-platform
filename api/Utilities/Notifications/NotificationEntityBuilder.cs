using Amanah.Api.Data.Entities;
using Amanah.Api.Services.Notifications;

namespace Amanah.Api.Utilities.Notifications;

public static class NotificationEntityBuilder
{
    public static Notification Create(
        Guid userId,
        string type,
        NotificationPayload payload,
        DateTimeOffset createdAt) =>
        new()
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Type = type,
            PayloadJson = payload.ToJson(),
            IsRead = false,
            CreatedAt = createdAt,
        };
}
