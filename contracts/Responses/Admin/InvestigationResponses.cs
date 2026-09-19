using Amanah.Contracts.Responses.Chats;

namespace Amanah.Contracts.Responses.Admin;

public sealed class InvestigationChatResponse
{
    public IReadOnlyList<ChatThreadDetailResponse> Threads { get; init; } = [];
}

public sealed class InvestigationClaimsResponse
{
    public IReadOnlyList<InvestigationClaimResponse> Items { get; init; } = [];
}

public sealed class InvestigationClaimResponse
{
    public required Guid Id { get; init; }

    public required string Status { get; init; }

    public required string SubmittedAnswer { get; init; }

    public bool HasPhoto { get; init; }

    public string? PhotoUrl { get; init; }

    public DateTimeOffset SubmittedAt { get; init; }

    public DateTimeOffset? ReviewedAt { get; init; }

    public string? DecisionReason { get; init; }

    public int AttemptNumber { get; init; }

    public required string ClaimantDisplayName { get; init; }

    public Guid? ChatThreadId { get; init; }
}

public sealed class InvestigationPhotosResponse
{
    public IReadOnlyList<InvestigationReportPhotoResponse> Photos { get; init; } = [];
}

public sealed class InvestigationReportPhotoResponse
{
    public required Guid Id { get; init; }

    public required string Url { get; init; }

    public int SortOrder { get; init; }
}
