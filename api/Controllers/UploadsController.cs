using Amanah.Api.Auth;
using Amanah.Api.Services.Uploads;
using Amanah.Contracts.Errors;
using Amanah.Contracts.Responses.Uploads;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Amanah.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/uploads")]
[Authorize]
public sealed class UploadsController(
    ReportPhotoPresignService reportPhotoPresignService,
    ClaimPhotoPresignService claimPhotoPresignService,
    ChatAttachmentAttachService chatAttachmentAttachService,
    ChatAttachmentPresignService chatAttachmentPresignService) : ControllerBase
{
    [HttpGet("report-photo/{id:guid}/url")]
    [EndpointName(nameof(GetReportPhotoUrl))]
    [EndpointSummary("Get a short-lived URL for a private report photo.")]
    [ProducesResponseType(typeof(ReportPhotoPresignResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetReportPhotoUrl(
        Guid id,
        CancellationToken cancellationToken)
    {
        User.TryGetUserId(out var userId);

        var result = await reportPhotoPresignService.GetReportPhotoUrlAsync(
            id,
            userId,
            User.GetUserRole(),
            cancellationToken);
        return result.ToActionResult();
    }

    [HttpGet("claim-photo/{id:guid}/url")]
    [EndpointName(nameof(GetClaimPhotoUrl))]
    [EndpointSummary("Get a short-lived URL for a claim photo.")]
    [ProducesResponseType(typeof(ClaimPhotoPresignResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetClaimPhotoUrl(
        Guid id,
        CancellationToken cancellationToken)
    {
        User.TryGetUserId(out var userId);

        var result = await claimPhotoPresignService.GetClaimPhotoUrlAsync(
            id,
            userId,
            User.GetUserRole(),
            cancellationToken);
        return result.ToActionResult();
    }

    [HttpPost("chat-attachment")]
    [Consumes("multipart/form-data")]
    [EnableRateLimiting("photo-upload")]
    [EndpointName(nameof(UploadChatAttachment))]
    [EndpointSummary("Upload a chat photo attachment for a thread.")]
    [ProducesResponseType(typeof(ChatAttachmentUploadResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> UploadChatAttachment(
        [FromForm] Guid threadId,
        IFormFile photo,
        CancellationToken cancellationToken)
    {
        User.TryGetUserId(out var userId);

        var result = await chatAttachmentAttachService.UploadAsync(
            threadId,
            userId,
            photo,
            cancellationToken);

        if (!result.IsSuccess)
        {
            return result.ToActionResult();
        }

        return StatusCode(StatusCodes.Status201Created, result.Value);
    }

    [HttpGet("chat-attachment/{id:guid}/url")]
    [EndpointName(nameof(GetChatAttachmentUrl))]
    [EndpointSummary("Get a short-lived URL for a chat attachment.")]
    [ProducesResponseType(typeof(ChatAttachmentPresignResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetChatAttachmentUrl(
        Guid id,
        CancellationToken cancellationToken)
    {
        User.TryGetUserId(out var userId);

        var result = await chatAttachmentPresignService.GetAttachmentUrlAsync(
            id,
            userId,
            cancellationToken);

        return result.ToActionResult();
    }
}
