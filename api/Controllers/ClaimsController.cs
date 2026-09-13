using Amanah.Api.Auth;
using Amanah.Api.Services.Claims;
using Amanah.Api.Services.Resolution;
using Amanah.Contracts.Errors;
using Amanah.Contracts.Requests.Claims;
using Amanah.Contracts.Responses.Browse;
using Amanah.Contracts.Responses.Claims;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Amanah.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/claims")]
[Authorize]
public sealed class ClaimsController(
    IClaimService claimService,
    ResolutionService resolutionService) : ControllerBase
{
    [HttpGet("mine")]
    [EndpointName(nameof(GetMyClaims))]
    [EndpointSummary("List the authenticated claimant's claims.")]
    [ProducesResponseType(typeof(PaginatedResponse<MyClaimSummaryResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetMyClaims(
        [FromQuery] MyClaimsQuery query,
        CancellationToken cancellationToken)
    {
        User.TryGetUserId(out var claimantId);

        var result = await claimService.GetMineAsync(claimantId, query, cancellationToken);
        return result.ToActionResult();
    }

    [HttpGet("{id:guid}")]
    [EndpointName(nameof(GetClaim))]
    [EndpointSummary("Get claim detail for the claimant or report reporter.")]
    [ProducesResponseType(typeof(ClaimDetailResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetClaim(
        Guid id,
        CancellationToken cancellationToken)
    {
        User.TryGetUserId(out var userId);

        var result = await claimService.GetByIdAsync(id, userId, cancellationToken);
        return result.ToActionResult();
    }

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

    [HttpPost("{id:guid}/withdraw")]
    [EndpointName(nameof(WithdrawClaim))]
    [EndpointSummary("Withdraw a pending claim as the claimant.")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> WithdrawClaim(Guid id, CancellationToken cancellationToken)
    {
        User.TryGetUserId(out var claimantId);

        var result = await claimService.WithdrawAsync(id, claimantId, cancellationToken);
        return result.ToActionResult();
    }

    [HttpPost("{id:guid}/confirm-resolution")]
    [EndpointName(nameof(ConfirmResolution))]
    [EndpointSummary("Confirm that the item was returned for an approved claim.")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ConfirmResolution(Guid id, CancellationToken cancellationToken)
    {
        User.TryGetUserId(out var userId);

        var result = await resolutionService.ConfirmResolutionAsync(id, userId, cancellationToken);
        return result.ToActionResult();
    }

    [HttpPost("{id:guid}/cancel")]
    [EndpointName(nameof(CancelClaim))]
    [EndpointSummary("Cancel an approved claim before mutual resolution confirmation.")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CancelClaim(Guid id, CancellationToken cancellationToken)
    {
        User.TryGetUserId(out var userId);

        var result = await resolutionService.CancelAsync(id, userId, cancellationToken);
        return result.ToActionResult();
    }
}
