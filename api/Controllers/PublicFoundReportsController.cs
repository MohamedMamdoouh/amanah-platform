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
[Route("api/v{version:apiVersion}/found")]
[AllowAnonymous]
public sealed class PublicFoundReportsController(BrowseService browseService) : ControllerBase
{
    [HttpGet("{id:guid}")]
    [EndpointName(nameof(GetFoundReport))]
    [EndpointSummary("Get public detail for a found report.")]
    [ProducesResponseType(typeof(PublicReportDetailResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status410Gone)]
    public async Task<IActionResult> GetFoundReport(
        Guid id,
        CancellationToken cancellationToken)
    {
        var result = await browseService.GetFoundDetailAsync(id, cancellationToken);
        return result.ToActionResult();
    }
}
