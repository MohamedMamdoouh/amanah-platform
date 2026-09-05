namespace Amanah.Contracts.Requests.Reports;

public sealed class CreateReportRequest
{
    public string Type { get; init; } = string.Empty;

    public string CategoryCode { get; init; } = string.Empty;

    public string Title { get; init; } = string.Empty;

    public string Description { get; init; } = string.Empty;

    public DateOnly DateLostOrFound { get; init; }

    public string GovernorateCode { get; init; } = string.Empty;

    public string? AreaText { get; init; }

    public string? HeldLocation { get; init; }

    public bool HasReward { get; init; }

    public int? RewardAmount { get; init; }

    public string HiddenDetail { get; init; } = string.Empty;

    public Dictionary<string, string> CategoryFields { get; init; } = [];
}

public sealed class UpdateReportRequest
{
    public string CategoryCode { get; init; } = string.Empty;

    public string Title { get; init; } = string.Empty;

    public string Description { get; init; } = string.Empty;

    public DateOnly DateLostOrFound { get; init; }

    public string GovernorateCode { get; init; } = string.Empty;

    public string? AreaText { get; init; }

    public string? HeldLocation { get; init; }

    public bool HasReward { get; init; }

    public int? RewardAmount { get; init; }

    public string HiddenDetail { get; init; } = string.Empty;

    public Dictionary<string, string> CategoryFields { get; init; } = [];
}

public sealed class WithdrawReportRequest
{
    public string? Reason { get; init; }
}
