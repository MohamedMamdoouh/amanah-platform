using Amanah.Api.Models.Errors;
using Amanah.Api.Services.Browse;
using Amanah.Contracts.Errors;
using Amanah.Contracts.Responses.Browse;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Amanah.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/lost")]
[AllowAnonymous]
public sealed class PublicLostReportsController(BrowseService browseService) : ControllerBase
{
    [HttpGet("{id:guid}")]
    [EndpointName(nameof(GetLostReport))]
    [EndpointSummary("Get public detail for a lost report.")]
    [ProducesResponseType(typeof(PublicReportDetailResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status410Gone)]
    public async Task<IActionResult> GetLostReport(
        Guid id,
        CancellationToken cancellationToken)
    {
        var result = await browseService.GetLostDetailAsync(id, cancellationToken);
        return result.ToActionResult();
    }
}
