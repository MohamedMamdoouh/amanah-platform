namespace Amanah.Contracts.Responses.Account;

public sealed class AccountDeactivationStatusResponse
{
    public bool CanDeactivate { get; init; }

    public IReadOnlyList<string> Blockers { get; init; } = [];

    public DateTimeOffset? DeactivatedAt { get; init; }
}
