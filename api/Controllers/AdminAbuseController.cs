using Amanah.Api.Auth;
using Amanah.Api.Services.Abuse;
using Amanah.Contracts.Errors;
using Amanah.Contracts.Requests.Admin;
using Amanah.Contracts.Responses.Admin;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Amanah.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/admin/abuse")]
[Authorize(AuthPolicies.Admin)]
public sealed class AdminAbuseController(AbuseAdminService abuseAdminService) : ControllerBase
{
    [HttpGet]
    [EndpointName(nameof(GetAbuseQueue))]
    [EndpointSummary("List open abuse reports in FIFO order.")]
    [ProducesResponseType(typeof(AbuseQueueResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetAbuseQueue(CancellationToken cancellationToken)
    {
        var result = await abuseAdminService.GetQueueAsync(cancellationToken);
        return result.ToActionResult();
    }

    [HttpGet("{id:guid}")]
    [EndpointName(nameof(GetAbuseReport))]
    [EndpointSummary("Get abuse report detail with flagged listing summary.")]
    [ProducesResponseType(typeof(AbuseReportDetailResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetAbuseReport(Guid id, CancellationToken cancellationToken)
    {
        var result = await abuseAdminService.GetDetailAsync(id, cancellationToken);
        return result.ToActionResult();
    }

    [HttpPost("{id:guid}/resolve")]
    [EndpointName(nameof(ResolveAbuseReport))]
    [EndpointSummary("Resolve an open abuse report with no action, takedown, or ban.")]
    [ProducesResponseType(typeof(ResolveAbuseReportResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ResolveAbuseReport(
        Guid id,
        [FromBody] ResolveAbuseReportRequest request,
        CancellationToken cancellationToken)
    {
        User.TryGetUserId(out var adminId);

        var result = await abuseAdminService.ResolveAsync(id, adminId, request, cancellationToken);
        return result.ToActionResult();
    }
}
