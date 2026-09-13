namespace Amanah.Api.Data.Entities;

public class ChatAttachment : IEntity
{
    public Guid Id { get; set; }

    public Guid ChatThreadId { get; set; }

    public ChatThread ChatThread { get; set; } = null!;

    public Guid UploaderId { get; set; }

    public User Uploader { get; set; } = null!;

    public required string StorageKey { get; set; }

    public required string ContentType { get; set; }

    public long SizeBytes { get; set; }

    public string? ThumbnailStorageKey { get; set; }

    public Guid? MessageId { get; set; }

    public Message? Message { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}
