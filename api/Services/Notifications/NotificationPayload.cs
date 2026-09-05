using System.Text.Json;
using Amanah.Api.Models.Common;

namespace Amanah.Api.Services.Notifications;

public sealed record NotificationPayload(
    string Type,
    DateTimeOffset CreatedAt,
    string DeepLink,
    Guid? ReportId = null,
    string? ReasonCode = null,
    string? Note = null)
{
    public string ToJson() =>
        JsonSerializer.Serialize(this, ApiJson.SerializerOptions);

    public static NotificationPayload FromJson(string json) =>
        JsonSerializer.Deserialize<NotificationPayload>(json, ApiJson.SerializerOptions)
        ?? throw new InvalidOperationException("Notification payload is invalid.");
}
