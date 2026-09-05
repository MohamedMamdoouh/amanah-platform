using Amanah.Api.Auth;
using Amanah.Contracts.Errors;
using Amanah.Contracts.Requests.Admin;
using Amanah.Contracts.Responses.Admin;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Amanah.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/admin/moderation")]
[Authorize(AuthPolicies.Admin)]
public sealed class AdminModerationController : ControllerBase
{
    [HttpGet("queue")]
    [EndpointName(nameof(GetModerationQueue))]
    [EndpointSummary("List pending reports in FIFO order.")]
    [ProducesResponseType(typeof(ModerationQueueResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status501NotImplemented)]
    public IActionResult GetModerationQueue() => NotImplemented();

    [HttpGet("reports/{id:guid}")]
    [EndpointName(nameof(GetModerationReport))]
    [EndpointSummary("Get a report for admin review.")]
    [ProducesResponseType(StatusCodes.Status501NotImplemented)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status403Forbidden)]
    public IActionResult GetModerationReport(Guid id) => NotImplemented();

    [HttpPost("reports/{id:guid}/approve")]
    [EndpointName(nameof(ApproveReport))]
    [EndpointSummary("Approve a pending report.")]
    [ProducesResponseType(StatusCodes.Status501NotImplemented)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status403Forbidden)]
    public IActionResult ApproveReport(Guid id) => NotImplemented();

    [HttpPost("reports/{id:guid}/reject")]
    [EndpointName(nameof(RejectReport))]
    [EndpointSummary("Reject a pending report.")]
    [ProducesResponseType(StatusCodes.Status501NotImplemented)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status403Forbidden)]
    public IActionResult RejectReport(Guid id, [FromBody] RejectReportRequest request) =>
        NotImplemented();

    [HttpGet("search")]
    [EndpointName(nameof(SearchModerationReports))]
    [EndpointSummary("Search pending and rejected reports by keyword.")]
    [ProducesResponseType(typeof(ModerationSearchResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status501NotImplemented)]
    public IActionResult SearchModerationReports([FromQuery] string? q) => NotImplemented();

    private ObjectResult NotImplemented() =>
        StatusCode(StatusCodes.Status501NotImplemented, new ApiError(
            ErrorCodes.NotImplemented,
            "This endpoint is not implemented yet."));
}
