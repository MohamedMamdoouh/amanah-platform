namespace Amanah.Contracts.Responses.Claims;

public sealed class SubmitClaimResponse
{
    public required Guid Id { get; init; }

    public required string Status { get; init; }
}
