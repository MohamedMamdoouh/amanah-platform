using System.Net.Http.Headers;
using System.Net.Http.Json;
using Amanah.Contracts.Responses.Uploads;

namespace Amanah.Api.Tests.Uploads;

public static class ChatAttachmentTestHelpers
{
    public static async Task<(HttpResponseMessage Response, ChatAttachmentUploadResponse? Body)> UploadAsync(
        HttpClient client,
        Guid threadId,
        byte[] photoContent,
        string photoContentType = "image/jpeg")
    {
        using var content = new MultipartFormDataContent();
        content.Add(new StringContent(threadId.ToString()), "threadId");

        var photoPart = new ByteArrayContent(photoContent);
        photoPart.Headers.ContentType = new MediaTypeHeaderValue(photoContentType);
        content.Add(photoPart, "photo", "chat-photo.jpg");

        var response = await client.PostAsync("/api/v1/uploads/chat-attachment", content);
        ChatAttachmentUploadResponse? body = response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<ChatAttachmentUploadResponse>()
            : null;

        return (response, body);
    }

    public static Task<HttpResponseMessage> GetPresignedUrlAsync(HttpClient client, Guid attachmentId) =>
        client.GetAsync($"/api/v1/uploads/chat-attachment/{attachmentId}/url");
}
