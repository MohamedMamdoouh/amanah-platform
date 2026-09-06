using Amanah.Api.Auth;
using Amanah.Api.Services.Admin;
using Amanah.Contracts.Errors;
using Amanah.Contracts.Requests.Admin;
using Amanah.Contracts.Responses.Admin;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Amanah.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/admin/categories")]
[Authorize(AuthPolicies.Admin)]
public sealed class AdminCategoriesController(CategoryAdminService categoryAdminService) : ControllerBase
{
    [HttpGet]
    [EndpointName(nameof(GetAdminCategories))]
    [EndpointSummary("List all categories including inactive ones.")]
    [ProducesResponseType(typeof(AdminCategoryListResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetAdminCategories(CancellationToken cancellationToken)
    {
        var result = await categoryAdminService.GetAllAsync(cancellationToken);
        return result.ToActionResult();
    }

    [HttpPost]
    [EndpointName(nameof(CreateCategory))]
    [EndpointSummary("Create a category.")]
    [ProducesResponseType(typeof(AdminCategoryResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateCategory(
        [FromBody] CreateCategoryRequest request,
        CancellationToken cancellationToken)
    {
        var result = await categoryAdminService.CreateCategoryAsync(request, cancellationToken);
        return result.ToActionResult();
    }

    [HttpPut("{id:guid}")]
    [EndpointName(nameof(UpdateCategory))]
    [EndpointSummary("Update a category.")]
    [ProducesResponseType(typeof(AdminCategoryResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> UpdateCategory(
        Guid id,
        [FromBody] UpdateCategoryRequest request,
        CancellationToken cancellationToken)
    {
        var result = await categoryAdminService.UpdateCategoryAsync(id, request, cancellationToken);
        return result.ToActionResult();
    }

    [HttpPost("{id:guid}/fields")]
    [EndpointName(nameof(CreateCategoryField))]
    [EndpointSummary("Add a field definition to a category.")]
    [ProducesResponseType(typeof(AdminCategoryFieldDefinitionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateCategoryField(
        Guid id,
        [FromBody] CreateCategoryFieldRequest request,
        CancellationToken cancellationToken)
    {
        var result = await categoryAdminService.CreateFieldAsync(id, request, cancellationToken);
        return result.ToActionResult();
    }

    [HttpPut("{id:guid}/fields/{fieldId:guid}")]
    [EndpointName(nameof(UpdateCategoryField))]
    [EndpointSummary("Update a category field definition.")]
    [ProducesResponseType(typeof(AdminCategoryFieldDefinitionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> UpdateCategoryField(
        Guid id,
        Guid fieldId,
        [FromBody] UpdateCategoryFieldRequest request,
        CancellationToken cancellationToken)
    {
        var result = await categoryAdminService.UpdateFieldAsync(id, fieldId, request, cancellationToken);
        return result.ToActionResult();
    }
}
