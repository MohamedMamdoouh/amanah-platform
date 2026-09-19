namespace Amanah.Contracts.Requests.Admin;

public sealed class ResolveAbuseReportRequest
{
    public string Outcome { get; init; } = string.Empty;

    public string? AdminNote { get; init; }

    public Guid? BanTargetUserId { get; init; }
}

public sealed class AdminReportTakedownRequest
{
    public string? Note { get; init; }
}
