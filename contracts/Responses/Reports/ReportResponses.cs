namespace Amanah.Contracts.Responses.Reports;

public sealed class CreateReportResponse
{
    public required Guid Id { get; init; }

    public required string Status { get; init; }
}

public sealed class ReportListResponse
{
    public IReadOnlyList<ReportSummaryResponse> Items { get; init; } = [];
}

public sealed class ReportSummaryResponse
{
    public required Guid Id { get; init; }

    public required string Type { get; init; }

    public required string Status { get; init; }

    public required string Title { get; init; }

    public required string CategoryCode { get; init; }

    public required string GovernorateCode { get; init; }

    public DateTimeOffset CreatedAt { get; init; }

    public bool HasReward { get; init; }

    public int? RewardAmount { get; init; }
}

public sealed class ReportDetailResponse
{
    public required Guid Id { get; init; }

    public required string Type { get; init; }

    public required string Status { get; init; }

    public required string Title { get; init; }

    public required string CategoryCode { get; init; }

    public required string GovernorateCode { get; init; }

    public DateTimeOffset CreatedAt { get; init; }

    public bool HasReward { get; init; }

    public int? RewardAmount { get; init; }

    public required string Description { get; init; }

    public DateOnly DateLostOrFound { get; init; }

    public string? AreaText { get; init; }

    public string? HeldLocation { get; init; }

    public IReadOnlyDictionary<string, string> CategoryFields { get; init; } = new Dictionary<string, string>();

    public string? HiddenDetail { get; init; }

    public string? WithdrawalReason { get; init; }

    public IReadOnlyList<ReportPhotoResponse> Photos { get; init; } = [];
}

public sealed class ReportPhotoResponse
{
    public required Guid Id { get; init; }

    public string? ThumbnailUrl { get; init; }

    public int SortOrder { get; init; }
}
