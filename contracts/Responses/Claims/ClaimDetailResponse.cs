namespace Amanah.Contracts.Responses.Claims;

public sealed class ClaimDetailResponse
{
    public required Guid Id { get; init; }

    public required string Status { get; init; }

    public required string SubmittedAnswer { get; init; }

    public bool HasPhoto { get; init; }

    public DateTimeOffset SubmittedAt { get; init; }

    public DateTimeOffset? ReviewedAt { get; init; }

    public string? ReviewerDecision { get; init; }

    public string? DecisionReason { get; init; }

    public int AttemptNumber { get; init; }

    public Guid? ChatThreadId { get; init; }

    public required Guid ReportId { get; init; }

    public required string ReportType { get; init; }

    public required string ReportStatus { get; init; }

    public required string ReportTitle { get; init; }

    public required string ClaimantDisplayName { get; init; }

    public required string ReporterDisplayName { get; init; }
}

public sealed class MyClaimSummaryResponse
{
    public required Guid Id { get; init; }

    public required string Status { get; init; }

    public DateTimeOffset SubmittedAt { get; init; }

    public DateTimeOffset? ReviewedAt { get; init; }

    public string? DecisionReason { get; init; }

    public required Guid ReportId { get; init; }

    public required string ReportType { get; init; }

    public required string ReportTitle { get; init; }

    public required string ReporterDisplayName { get; init; }
}
