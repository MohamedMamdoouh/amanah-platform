using Amanah.Api.Auth;
using Amanah.Api.Services.Chats;
using Amanah.Contracts.Chats;
using Amanah.Contracts.Errors;
using Amanah.Contracts.Requests.Chats;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.SignalR;

namespace Amanah.Api.Hubs;

[Authorize(Policy = AuthPolicies.ActiveAccount)]
public sealed class ChatHub(ChatService chatService, ChatPresenceTracker presenceTracker) : Hub
{
    public async Task JoinThread(string threadId)
    {
        if (!Guid.TryParse(threadId, out var parsedThreadId))
        {
            throw new HubException(ErrorCodes.ValidationFailed);
        }

        if (!Context.User!.TryGetUserId(out var userId))
        {
            throw new HubException(ErrorCodes.Unauthorized);
        }

        if (!await chatService.IsParticipantAsync(parsedThreadId, userId, Context.ConnectionAborted))
        {
            throw new HubException(ErrorCodes.NotFound);
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
            throw new HubException(ErrorCodes.ValidationFailed);
        }

        if (!Context.User!.TryGetUserId(out var userId))
        {
            throw new HubException(ErrorCodes.Unauthorized);
        }

        Guid? parsedAttachmentId = null;
        if (!string.IsNullOrWhiteSpace(attachmentId))
        {
            if (!Guid.TryParse(attachmentId, out var attachmentGuid))
            {
                throw new HubException(ErrorCodes.NotFound);
            }

            parsedAttachmentId = attachmentGuid;
        }

        var result = await chatService.SendMessageAsync(
            parsedThreadId,
            userId,
            Context.User!.GetUserRole(),
            new SendMessageRequest
            {
                Body = body,
                AttachmentId = parsedAttachmentId,
            },
            Context.ConnectionAborted);

        if (!result.IsSuccess)
        {
            throw new HubException(result.Error!.Code);
        }

        // Message is already sent to the group & handled by the ChatService
    }

    public override Task OnDisconnectedAsync(Exception? exception)
    {
        presenceTracker.RemoveConnection(Context.ConnectionId);
        return base.OnDisconnectedAsync(exception);
    }

}
