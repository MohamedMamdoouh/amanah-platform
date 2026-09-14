using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Amanah.Api.Tests.Claims;
using Amanah.Api.Tests.Infrastructure;
using Amanah.Contracts.Chats;
using Amanah.Contracts.Requests.Chats;
using Amanah.Contracts.Responses.Chats;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.SignalR.Client;

namespace Amanah.Api.Tests.Chats;

public static class ChatTestHelpers
{
    public static async Task<Guid> GetThreadIdAsync(HttpClient client, Guid claimId)
    {
        var (_, claim) = await ClaimTestHelpers.GetClaimAsync(client, claimId);
        if (claim?.ChatThreadId is null)
        {
            throw new InvalidOperationException("Claim does not have a chat thread.");
        }

        return claim.ChatThreadId.Value;
    }

    public static Task<HttpResponseMessage> ListChatsAsync(HttpClient client) =>
        client.GetAsync("/api/v1/chats");

    public static Task<HttpResponseMessage> GetThreadAsync(
        HttpClient client,
        Guid threadId,
        Guid? before = null,
        int? limit = null)
    {
        var query = new List<string>();
        if (before is Guid beforeMessageId)
        {
            query.Add($"before={beforeMessageId}");
        }

        if (limit is int messageLimit)
        {
            query.Add($"limit={messageLimit}");
        }

        var suffix = query.Count == 0 ? string.Empty : $"?{string.Join('&', query)}";
        return client.GetAsync($"/api/v1/chats/{threadId}{suffix}");
    }

    public static async Task<(HttpResponseMessage Response, ChatMessageResponse? Body)> SendMessageAsync(
        HttpClient client,
        Guid threadId,
        SendMessageRequest request)
    {
        var response = await client.PostAsync(
            $"/api/v1/chats/{threadId}/messages",
            new StringContent(
                JsonSerializer.Serialize(request),
                Encoding.UTF8,
                "application/json"));

        ChatMessageResponse? body = response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<ChatMessageResponse>()
            : null;

        return (response, body);
    }

    public static Task<(HttpResponseMessage Response, ChatMessageResponse? Body)> SendMessageAsync(
        HttpClient client,
        Guid threadId,
        string body) =>
        SendMessageAsync(client, threadId, new SendMessageRequest { Body = body });

    public static async Task<HubConnection> ConnectHubAsync(
        WebApplicationFactory<ApiAssemblyMarker> factory,
        string accessToken)
    {
        var connection = new HubConnectionBuilder()
            .WithUrl(new Uri(factory.Server.BaseAddress!, ChatHubRoutes.Path.TrimStart('/')), options =>
            {
                options.AccessTokenProvider = () => Task.FromResult<string?>(accessToken);
                options.HttpMessageHandlerFactory = _ => factory.Server.CreateHandler();
            })
            .WithAutomaticReconnect()
            .Build();

        await connection.StartAsync();
        return connection;
    }
}
