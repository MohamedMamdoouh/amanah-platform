namespace Amanah.Contracts.Responses.Admin;

public sealed class ModerationQueueResponse
{
    public IReadOnlyList<ModerationQueueItemResponse> Items { get; init; } = [];

    public int PendingCount { get; init; }
}

public sealed class ModerationQueueItemResponse
{
    public required Guid Id { get; init; }

    public required string Type { get; init; }

    public required string Title { get; init; }

    public required string CategoryCode { get; init; }

    public required string Status { get; init; }

    public DateTimeOffset CreatedAt { get; init; }
}

public sealed class ModerationSearchResponse
{
    public IReadOnlyList<ModerationQueueItemResponse> Items { get; init; } = [];
}
