namespace Amanah.Contracts.Responses.Admin;

public sealed class AbuseQueueResponse
{
    public IReadOnlyList<AbuseQueueItemResponse> Items { get; init; } = [];

    public int OpenCount { get; init; }
}

public sealed class AbuseQueueItemResponse
{
    public required Guid Id { get; init; }

    public required Guid ReportId { get; init; }

    public required string ReportType { get; init; }

    public required string ReportTitle { get; init; }

    public required string ReportStatus { get; init; }

    public required string Reason { get; init; }

    public required Guid AbuseReporterUserId { get; init; }

    public required string AbuseReporterDisplayName { get; init; }

    public required string Status { get; init; }

    public DateTimeOffset CreatedAt { get; init; }
}

public sealed class AbuseReportDetailResponse
{
    public required Guid Id { get; init; }

    public required string Reason { get; init; }

    public string? Note { get; init; }

    public required string Status { get; init; }

    public string? ResolutionOutcome { get; init; }

    public DateTimeOffset CreatedAt { get; init; }

    public DateTimeOffset? ResolvedAt { get; init; }

    public required Guid AbuseReporterUserId { get; init; }

    public required string AbuseReporterDisplayName { get; init; }

    public required FlaggedListingSummaryResponse Listing { get; init; }
}

public sealed class FlaggedListingSummaryResponse
{
    public required Guid Id { get; init; }

    public required string Type { get; init; }

    public required string Status { get; init; }

    public required string Title { get; init; }

    public required string CategoryCode { get; init; }

    public required string GovernorateCode { get; init; }

    public required Guid ListingOwnerUserId { get; init; }

    public required string ListingOwnerDisplayName { get; init; }

    public DateTimeOffset CreatedAt { get; init; }

    public DateTimeOffset? PublishedAt { get; init; }
}

public sealed class ResolveAbuseReportResponse
{
    public required Guid Id { get; init; }

    public required string Status { get; init; }

    public required string ResolutionOutcome { get; init; }

    public DateTimeOffset ResolvedAt { get; init; }
}

public sealed class AdminReportTakedownResponse
{
    public required Guid ReportId { get; init; }

    public required string Status { get; init; }
}
