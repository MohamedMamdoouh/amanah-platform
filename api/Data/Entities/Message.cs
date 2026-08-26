namespace Amanah.Api.Data.Entities;

public class Message : IEntity
{
    public Guid Id { get; set; }

    public Guid ChatThreadId { get; set; }

    public ChatThread ChatThread { get; set; } = null!;

    public Guid SenderId { get; set; }

    public User Sender { get; set; } = null!;

    public required string Body { get; set; }

    public string? AttachmentStorageKey { get; set; }

    public DateTimeOffset SentAt { get; set; }
}
