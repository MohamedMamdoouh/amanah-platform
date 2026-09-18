using Amanah.Api.Auth;
using Amanah.Api.Services.Chats;
using Amanah.Contracts.Errors;
using Amanah.Contracts.Requests.Chats;
using Amanah.Contracts.Responses.Chats;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Amanah.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/chats")]
[Authorize(Policy = AuthPolicies.ActiveAccount)]
public sealed class ChatsController(ChatService chatService) : ControllerBase
{
    [HttpGet]
    [EndpointName(nameof(GetMyChats))]
    [EndpointSummary("List chat threads for the authenticated user.")]
    [ProducesResponseType(typeof(ChatThreadListResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetMyChats(CancellationToken cancellationToken)
    {
        User.TryGetUserId(out var userId);

        var result = await chatService.ListThreadsAsync(userId, cancellationToken);
        return result.ToActionResult();
    }

    [HttpGet("{threadId:guid}")]
    [EndpointName(nameof(GetChatThread))]
    [EndpointSummary("Get chat thread metadata and message history.")]
    [ProducesResponseType(typeof(ChatThreadDetailResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetChatThread(
        Guid threadId,
        [FromQuery] Guid? before,
        [FromQuery] int? limit,
        CancellationToken cancellationToken)
    {
        User.TryGetUserId(out var userId);

        var result = await chatService.GetThreadAsync(
            threadId,
            userId,
            before,
            limit,
            cancellationToken);

        return result.ToActionResult();
    }

    [HttpPost("{threadId:guid}/messages")]
    [EnableRateLimiting("chat-message")]
    [EndpointName(nameof(SendChatMessage))]
    [EndpointSummary("Send a chat message via REST fallback.")]
    [ProducesResponseType(typeof(ChatMessageResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> SendChatMessage(
        Guid threadId,
        [FromBody] SendMessageRequest request,
        CancellationToken cancellationToken)
    {
        User.TryGetUserId(out var userId);

        var result = await chatService.SendMessageAsync(threadId, userId, request, cancellationToken);
        if (!result.IsSuccess)
        {
            return result.ToActionResult();
        }

        return StatusCode(StatusCodes.Status201Created, result.Value);
    }
}
