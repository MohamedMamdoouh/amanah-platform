using Amanah.Api.Models.Errors;
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
        var result = await browseService.ListReportsAsync(query, cancellationToken);
        return result.ToActionResult();
    }
}
