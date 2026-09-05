namespace Amanah.Contracts.Requests.Admin;

public sealed class RejectReportRequest
{
    public string ReasonCode { get; init; } = string.Empty;

    public string? Note { get; init; }
}
