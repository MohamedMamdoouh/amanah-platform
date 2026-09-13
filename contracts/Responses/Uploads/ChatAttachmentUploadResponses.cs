namespace Amanah.Contracts.Responses.Uploads;

public sealed class ChatAttachmentUploadResponse
{
    public required Guid Id { get; init; }
}

public sealed class ChatAttachmentPresignResponse
{
    public required string Url { get; init; }
}
