namespace Amanah.Contracts.Responses.Account;

public sealed class AccountDeletionStatusResponse
{
    public bool CanDelete { get; init; }

    public IReadOnlyList<string> Blockers { get; init; } = [];

    public DateTimeOffset? DeletionRequestedAt { get; init; }
}
