namespace Amanah.Contracts.Requests.Chats;

public sealed class SendMessageRequest
{
    public string? Body { get; init; }

    public Guid? AttachmentId { get; init; }
}
