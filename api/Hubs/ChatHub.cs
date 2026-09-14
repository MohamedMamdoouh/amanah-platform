using Amanah.Api.Auth;
using Amanah.Api.Services.Chats;
using Amanah.Contracts.Chats;
using Amanah.Contracts.Requests.Chats;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.SignalR;

namespace Amanah.Api.Hubs;

[Authorize]
public sealed class ChatHub(ChatService chatService, ChatPresenceTracker presenceTracker) : Hub
{
    public async Task JoinThread(string threadId)
    {
        if (!Guid.TryParse(threadId, out var parsedThreadId))
        {
            throw new HubException("Invalid thread id.");
        }

        if (!Context.User!.TryGetUserId(out var userId))
        {
            throw new HubException("Unauthorized.");
        }

        if (!await chatService.IsParticipantAsync(parsedThreadId, userId, Context.ConnectionAborted))
        {
            throw new HubException("Chat thread not found.");
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, ChatHubGroups.ForThread(parsedThreadId));
        presenceTracker.Join(Context.ConnectionId, userId, parsedThreadId);
    }

    public async Task LeaveThread(string threadId)
    {
        if (!Guid.TryParse(threadId, out var parsedThreadId))
        {
            return;
        }

        await Groups.RemoveFromGroupAsync(Context.ConnectionId, ChatHubGroups.ForThread(parsedThreadId));
        presenceTracker.Leave(Context.ConnectionId, parsedThreadId);
    }

    [EnableRateLimiting("chat-message")]
    public async Task SendMessage(string threadId, string? body, string? attachmentId)
    {
        if (!Guid.TryParse(threadId, out var parsedThreadId))
        {
            throw new HubException("Invalid thread id.");
        }

        if (!Context.User!.TryGetUserId(out var userId))
        {
            throw new HubException("Unauthorized.");
        }

        Guid? parsedAttachmentId = null;
        if (!string.IsNullOrWhiteSpace(attachmentId))
        {
            if (!Guid.TryParse(attachmentId, out var attachmentGuid))
            {
                throw new HubException("Attachment not found.");
            }

            parsedAttachmentId = attachmentGuid;
        }

        var result = await chatService.SendMessageAsync(
            parsedThreadId,
            userId,
            new SendMessageRequest
            {
                Body = body,
                AttachmentId = parsedAttachmentId,
            },
            Context.ConnectionAborted);

        if (!result.IsSuccess)
        {
            throw new HubException(result.Error!.Message);
        }
    }

    public override Task OnDisconnectedAsync(Exception? exception)
    {
        presenceTracker.RemoveConnection(Context.ConnectionId);
        return base.OnDisconnectedAsync(exception);
    }

}
