using Amanah.Api.Auth;
using Amanah.Api.Services.Abuse;
using Amanah.Contracts.Errors;
using Amanah.Contracts.Responses.Admin;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Amanah.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/admin/investigations")]
[Authorize(AuthPolicies.Admin)]
public sealed class AdminInvestigationsController(
    FlaggedListingInvestigationService investigationService) : ControllerBase
{
    [HttpGet("{reportId:guid}/chat")]
    [EndpointName(nameof(GetInvestigationChat))]
    [EndpointSummary("Get chat threads and messages for a flagged listing investigation.")]
    [ProducesResponseType(typeof(InvestigationChatResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetInvestigationChat(
        Guid reportId,
        CancellationToken cancellationToken)
    {
        var result = await investigationService.GetChatAsync(reportId, cancellationToken);
        return result.ToActionResult();
    }

    [HttpGet("{reportId:guid}/claims")]
    [EndpointName(nameof(GetInvestigationClaims))]
    [EndpointSummary("Get claim text and photo URLs for a flagged listing investigation.")]
    [ProducesResponseType(typeof(InvestigationClaimsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetInvestigationClaims(
        Guid reportId,
        CancellationToken cancellationToken)
    {
        var result = await investigationService.GetClaimsAsync(reportId, cancellationToken);
        return result.ToActionResult();
    }

    [HttpGet("{reportId:guid}/photos")]
    [EndpointName(nameof(GetInvestigationPhotos))]
    [EndpointSummary("Get private report photo URLs for a flagged listing investigation.")]
    [ProducesResponseType(typeof(InvestigationPhotosResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetInvestigationPhotos(
        Guid reportId,
        CancellationToken cancellationToken)
    {
        var result = await investigationService.GetPhotosAsync(reportId, cancellationToken);
        return result.ToActionResult();
    }
}
