namespace Amanah.Api.Data.Entities;

public class StorageDeletionOutboxMessage : IEntity
{
    public Guid Id { get; set; }

    public required string StorageKey { get; set; }

    public StorageDeletionOutboxStatus Status { get; set; }

    public StorageDeletionSource Source { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? ProcessedAt { get; set; }

    public int AttemptCount { get; set; }

    public string? LastError { get; set; }
}
