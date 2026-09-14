using Amanah.Contracts.Responses.Claims;

namespace Amanah.Contracts.Responses.Chats;

public sealed class ChatThreadListResponse
{
    public IReadOnlyList<ChatThreadSummaryResponse> Items { get; init; } = [];
}

public sealed class ChatThreadSummaryResponse
{
    public required Guid Id { get; init; }

    public required Guid ClaimId { get; init; }

    public required Guid ReportId { get; init; }

    public required string ReportTitle { get; init; }

    public required string ReportType { get; init; }

    public required string CounterpartyDisplayName { get; init; }

    public DateTimeOffset CreatedAt { get; init; }

    public DateTimeOffset? ReadOnlyAt { get; init; }

    public DateTimeOffset? LastMessageAt { get; init; }

    public string? LastMessagePreview { get; init; }
}

public sealed class ChatThreadDetailResponse
{
    public required Guid Id { get; init; }

    public required Guid ClaimId { get; init; }

    public required Guid ReportId { get; init; }

    public required string ReportTitle { get; init; }

    public required string ReportType { get; init; }

    public required string ReportStatus { get; init; }

    public required string ClaimStatus { get; init; }

    public required string CounterpartyDisplayName { get; init; }

    public DateTimeOffset CreatedAt { get; init; }

    public DateTimeOffset? ReadOnlyAt { get; init; }

    public ResolutionStateResponse? Resolution { get; init; }

    public IReadOnlyList<ChatMessageResponse> Messages { get; init; } = [];
}

public sealed class ChatMessageResponse
{
    public required Guid Id { get; init; }

    public required Guid ThreadId { get; init; }

    public required Guid SenderId { get; init; }

    public required string SenderDisplayName { get; init; }

    public required string Body { get; init; }

    public Guid? AttachmentId { get; init; }

    public DateTimeOffset SentAt { get; init; }
}

public sealed class ChatThreadReadOnlyResponse
{
    public required Guid ThreadId { get; init; }

    public required DateTimeOffset ReadOnlyAt { get; init; }
}
