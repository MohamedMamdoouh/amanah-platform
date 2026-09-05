using Amanah.Api.Auth;
using Amanah.Api.Services.Notifications;
using Amanah.Contracts.Errors;
using Amanah.Contracts.Responses.Notifications;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Amanah.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/notifications")]
[Authorize]
public sealed class NotificationsController(NotificationService notificationService) : ControllerBase
{
    [HttpGet]
    [EndpointName(nameof(GetNotifications))]
    [EndpointSummary("List notifications for the current user.")]
    [ProducesResponseType(typeof(NotificationListResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetNotifications(CancellationToken cancellationToken)
    {
        User.TryGetUserId(out var userId);

        var result = await notificationService.GetMineAsync(userId, cancellationToken);
        return result.ToActionResult();
    }

    [HttpGet("unread-count")]
    [EndpointName(nameof(GetUnreadNotificationCount))]
    [EndpointSummary("Get unread notification count for the header badge.")]
    [ProducesResponseType(typeof(NotificationUnreadCountResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetUnreadNotificationCount(CancellationToken cancellationToken)
    {
        User.TryGetUserId(out var userId);

        var result = await notificationService.GetUnreadCountAsync(userId, cancellationToken);
        return result.ToActionResult();
    }

    [HttpPatch("{id:guid}/read")]
    [EndpointName(nameof(MarkNotificationRead))]
    [EndpointSummary("Mark a notification as read.")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> MarkNotificationRead(
        Guid id,
        CancellationToken cancellationToken)
    {
        User.TryGetUserId(out var userId);

        var result = await notificationService.MarkReadAsync(id, userId, cancellationToken);
        return result.ToActionResult();
    }

    [HttpPost("read-all")]
    [EndpointName(nameof(MarkAllNotificationsRead))]
    [EndpointSummary("Mark all notifications as read.")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> MarkAllNotificationsRead(CancellationToken cancellationToken)
    {
        User.TryGetUserId(out var userId);

        var result = await notificationService.MarkAllReadAsync(userId, cancellationToken);
        return result.ToActionResult();
    }
}
