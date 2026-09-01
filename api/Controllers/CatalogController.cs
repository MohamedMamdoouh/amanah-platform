using Amanah.Api.Services.Catalog;
using Amanah.Contracts.Responses.Catalog;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Amanah.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}")]
[AllowAnonymous]
public sealed class CatalogController(CatalogService catalogService) : ControllerBase
{
    [HttpGet("categories")]
    [EndpointName(nameof(GetCategories))]
    [EndpointSummary("List active report categories with field definitions (keys only).")]
    [ProducesResponseType(typeof(CategoryListResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetCategories(CancellationToken cancellationToken)
    {
        var categories = await catalogService.GetCategoriesAsync(cancellationToken);
        return Ok(categories);
    }

    [HttpGet("governorates")]
    [EndpointName(nameof(GetGovernorates))]
    [EndpointSummary("List Egyptian governorates (keys only).")]
    [ProducesResponseType(typeof(GovernorateListResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetGovernorates(CancellationToken cancellationToken)
    {
        var governorates = await catalogService.GetGovernoratesAsync(cancellationToken);
        return Ok(governorates);
    }
}
