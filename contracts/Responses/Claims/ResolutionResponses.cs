namespace Amanah.Contracts.Responses.Claims;

public sealed class ResolutionStateResponse
{
    public DateTimeOffset? ReporterConfirmedAt { get; init; }

    public DateTimeOffset? ClaimantConfirmedAt { get; init; }

    public DateTimeOffset? ResolvedAt { get; init; }

    public bool CurrentUserHasConfirmed { get; init; }

    public bool CurrentUserCanCancel { get; init; }
}
