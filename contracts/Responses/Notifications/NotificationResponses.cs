namespace Amanah.Contracts.Responses.Notifications;

public sealed class NotificationListResponse
{
    public IReadOnlyList<NotificationItemResponse> Items { get; init; } = [];
}

public sealed class NotificationItemResponse
{
    public required Guid Id { get; init; }

    public required NotificationPayloadResponse Payload { get; init; }

    public bool IsRead { get; init; }
}

public sealed class NotificationPayloadResponse
{
    public required string Type { get; init; }

    public DateTimeOffset CreatedAt { get; init; }

    public required string DeepLink { get; init; }

    public Guid? ReportId { get; init; }

    public string? ReasonCode { get; init; }

    public string? Note { get; init; }
}

public sealed class NotificationUnreadCountResponse
{
    public int Count { get; init; }
}
