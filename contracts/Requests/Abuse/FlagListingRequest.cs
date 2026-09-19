namespace Amanah.Contracts.Requests.Abuse;

public sealed class FlagListingRequest
{
    public string Reason { get; init; } = string.Empty;

    public string? Note { get; init; }
}
