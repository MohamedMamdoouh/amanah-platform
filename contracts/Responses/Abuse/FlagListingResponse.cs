namespace Amanah.Contracts.Responses.Abuse;

public sealed class FlagListingResponse
{
    public required Guid Id { get; init; }

    public required Guid ReportId { get; init; }

    public required string Reason { get; init; }

    public string? Note { get; init; }

    public required string Status { get; init; }

    public DateTimeOffset CreatedAt { get; init; }
}
