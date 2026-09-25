using Amanah.Api.Auth;
using Amanah.Api.Services.Browse;
using Amanah.Contracts.Errors;
using Amanah.Contracts.Requests.Browse;
using Amanah.Contracts.Responses.Browse;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Amanah.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/reports")]
[AllowAnonymous]
public sealed class PublicReportsController(BrowseService browseService) : ControllerBase
{
    [HttpGet]
    [EndpointName(nameof(GetPublicReports))]
    [EndpointSummary("Browse and search published reports.")]
    [ProducesResponseType(typeof(PaginatedResponse<PublicReportSummaryResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetPublicReports(
        [FromQuery] BrowseReportsQuery query,
        CancellationToken cancellationToken)
    {
        Guid? viewerId = User.TryGetUserId(out var userId) ? userId : null;
        var result = await browseService.ListReportsAsync(query, viewerId, cancellationToken);
        return result.ToActionResult();
    }

    [HttpGet("{id:guid}/public")]
    [EndpointName(nameof(GetPublicReport))]
    [EndpointSummary("Get public report detail by ID.")]
    [ProducesResponseType(typeof(PublicReportDetailResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status410Gone)]
    public async Task<IActionResult> GetPublicReport(
        Guid id,
        CancellationToken cancellationToken)
    {
        var result = await browseService.GetPublicDetailAsync(id, cancellationToken);
        return result.ToActionResult();
    }
}
