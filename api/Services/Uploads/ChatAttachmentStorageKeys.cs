namespace Amanah.Api.Services.Uploads;

public static class ChatAttachmentStorageKeys
{
    private const string Prefix = "private/chat/";

    public static string Original(Guid threadId, Guid uploadId) =>
        $"{Prefix}{threadId:N}/{uploadId:N}";

    public static string Thumbnail(Guid threadId, Guid uploadId) =>
        $"{Original(threadId, uploadId)}_thumb.webp";

    public static string ThumbnailForOriginal(string originalKey) =>
        $"{originalKey}_thumb.webp";
}
