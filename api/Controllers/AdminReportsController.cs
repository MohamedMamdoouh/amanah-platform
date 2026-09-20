using Amanah.Api.Auth;
using Amanah.Api.Services.Enforcement;
using Amanah.Contracts.Errors;
using Amanah.Contracts.Requests.Admin;
using Amanah.Contracts.Responses.Admin;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Amanah.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/admin/reports")]
[Authorize(AuthPolicies.Admin)]
public sealed class AdminReportsController(AdminTakedownService adminTakedownService) : ControllerBase
{
    [HttpPost("{id:guid}/takedown")]
    [EndpointName(nameof(TakeDownReport))]
    [EndpointSummary("Remove a published listing or claim-in-progress report from public view.")]
    [ProducesResponseType(typeof(AdminReportTakedownResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> TakeDownReport(
        Guid id,
        [FromBody] AdminReportTakedownRequest request,
        CancellationToken cancellationToken)
    {
        User.TryGetUserId(out var adminId);

        var result = await adminTakedownService.TakeDownAsync(id, adminId, request, cancellationToken);
        return result.ToActionResult();
    }
}
