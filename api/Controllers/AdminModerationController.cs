using Amanah.Api.Auth;
using Amanah.Api.Services.Moderation;
using Amanah.Contracts.Errors;
using Amanah.Contracts.Requests.Admin;
using Amanah.Contracts.Responses.Admin;
using Amanah.Contracts.Responses.Reports;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Amanah.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/admin/moderation")]
[Authorize(AuthPolicies.Admin)]
public sealed class AdminModerationController(ModerationService moderationService) : ControllerBase
{
    [HttpGet("queue")]
    [EndpointName(nameof(GetModerationQueue))]
    [EndpointSummary("List pending reports in FIFO order.")]
    [ProducesResponseType(typeof(ModerationQueueResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetModerationQueue(CancellationToken cancellationToken)
    {
        var result = await moderationService.GetQueueAsync(cancellationToken);
        return result.ToActionResult();
    }

    [HttpGet("reports/{id:guid}")]
    [EndpointName(nameof(GetModerationReport))]
    [EndpointSummary("Get a report for admin review.")]
    [ProducesResponseType(typeof(ReportDetailResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetModerationReport(
        Guid id,
        CancellationToken cancellationToken)
    {
        User.TryGetUserId(out var adminId);

        var result = await moderationService.GetReportAsync(id, adminId, cancellationToken);
        return result.ToActionResult();
    }

    [HttpPost("reports/{id:guid}/approve")]
    [EndpointName(nameof(ApproveReport))]
    [EndpointSummary("Approve a pending report.")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ApproveReport(Guid id, CancellationToken cancellationToken)
    {
        User.TryGetUserId(out var adminId);

        var result = await moderationService.ApproveAsync(id, adminId, cancellationToken);
        return result.ToActionResult();
    }

    [HttpPost("reports/{id:guid}/reject")]
    [EndpointName(nameof(RejectReport))]
    [EndpointSummary("Reject a pending report.")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> RejectReport(
        Guid id,
        [FromBody] RejectReportRequest request,
        CancellationToken cancellationToken)
    {
        User.TryGetUserId(out var adminId);

        var result = await moderationService.RejectAsync(id, adminId, request, cancellationToken);
        return result.ToActionResult();
    }

    [HttpGet("search")]
    [EndpointName(nameof(SearchModerationReports))]
    [EndpointSummary("Search pending and rejected reports by keyword.")]
    [ProducesResponseType(typeof(ModerationSearchResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status501NotImplemented)]
    public IActionResult SearchModerationReports([FromQuery] string? q) =>
        StatusCode(StatusCodes.Status501NotImplemented, new ApiError(
            ErrorCodes.NotImplemented,
            "This endpoint is not implemented yet."));
}
