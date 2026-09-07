using Amanah.Contracts.Responses.Reports;

namespace Amanah.Contracts.Responses.Browse;

public sealed class PaginatedResponse<T>
{
    public required IReadOnlyList<T> Items { get; init; }

    public required int Page { get; init; }

    public required int PageSize { get; init; }

    public required int TotalCount { get; init; }

    public required int TotalPages { get; init; }
}

public sealed class PublicReportSummaryResponse
{
    public required Guid Id { get; init; }

    public required string Type { get; init; }

    public required string Status { get; init; }

    public required string Title { get; init; }

    public required string CategoryCode { get; init; }

    public required string GovernorateCode { get; init; }

    public DateTimeOffset? PublishedAt { get; init; }

    public bool HasReward { get; init; }

    public int? RewardAmount { get; init; }

    public required string ReporterDisplayName { get; init; }

    public string? ThumbnailUrl { get; init; }

    public string? AreaText { get; init; }
}

public sealed class PublicReportDetailResponse
{
    public required Guid Id { get; init; }

    public required string Type { get; init; }

    public required string Status { get; init; }

    public required string Title { get; init; }

    public required string CategoryCode { get; init; }

    public required string GovernorateCode { get; init; }

    public DateTimeOffset? PublishedAt { get; init; }

    public required string Description { get; init; }

    public DateOnly DateLostOrFound { get; init; }

    public string? AreaText { get; init; }

    public string? HeldLocation { get; init; }

    public bool HasReward { get; init; }

    public int? RewardAmount { get; init; }

    public required string ReporterDisplayName { get; init; }

    public IReadOnlyDictionary<string, string> CategoryFields { get; init; } =
        new Dictionary<string, string>();

    public IReadOnlyList<ReportPhotoResponse> Photos { get; init; } = [];
}
