using Amanah.Api.Auth;
using Amanah.Api.Services.Claims;
using Amanah.Contracts.Errors;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Amanah.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/claims")]
[Authorize]
public sealed class ClaimsController(IClaimService claimService) : ControllerBase
{
    [HttpPost("{id:guid}/approve")]
    [EndpointName(nameof(ApproveClaim))]
    [EndpointSummary("Approve a pending claim and move the report to claim in progress.")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ApproveClaim(Guid id, CancellationToken cancellationToken)
    {
        User.TryGetUserId(out var reporterId);

        var result = await claimService.ApproveAsync(id, reporterId, cancellationToken);
        return result.ToActionResult();
    }

    [HttpPost("{id:guid}/reject")]
    [EndpointName(nameof(RejectClaim))]
    [EndpointSummary("Reject a pending claim.")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> RejectClaim(Guid id, CancellationToken cancellationToken)
    {
        User.TryGetUserId(out var reporterId);

        var result = await claimService.RejectAsync(id, reporterId, cancellationToken);
        return result.ToActionResult();
    }
}
