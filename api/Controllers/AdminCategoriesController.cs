using Amanah.Api.Auth;
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
public sealed class AdminCategoriesController : ControllerBase
{
    [HttpGet]
    [EndpointName(nameof(GetAdminCategories))]
    [EndpointSummary("List all categories including inactive ones.")]
    [ProducesResponseType(typeof(AdminCategoryListResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status501NotImplemented)]
    public IActionResult GetAdminCategories() => NotImplemented();

    [HttpPost]
    [EndpointName(nameof(CreateCategory))]
    [EndpointSummary("Create a category.")]
    [ProducesResponseType(StatusCodes.Status501NotImplemented)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status403Forbidden)]
    public IActionResult CreateCategory([FromBody] CreateCategoryRequest request) => NotImplemented();

    [HttpPut("{id:guid}")]
    [EndpointName(nameof(UpdateCategory))]
    [EndpointSummary("Update a category.")]
    [ProducesResponseType(StatusCodes.Status501NotImplemented)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status403Forbidden)]
    public IActionResult UpdateCategory(Guid id, [FromBody] UpdateCategoryRequest request) =>
        NotImplemented();

    [HttpPost("{id:guid}/fields")]
    [EndpointName(nameof(CreateCategoryField))]
    [EndpointSummary("Add a field definition to a category.")]
    [ProducesResponseType(StatusCodes.Status501NotImplemented)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status403Forbidden)]
    public IActionResult CreateCategoryField(Guid id, [FromBody] CreateCategoryFieldRequest request) =>
        NotImplemented();

    [HttpPut("{id:guid}/fields/{fieldId:guid}")]
    [EndpointName(nameof(UpdateCategoryField))]
    [EndpointSummary("Update a category field definition.")]
    [ProducesResponseType(StatusCodes.Status501NotImplemented)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status403Forbidden)]
    public IActionResult UpdateCategoryField(
        Guid id,
        Guid fieldId,
        [FromBody] UpdateCategoryFieldRequest request) => NotImplemented();

    private ObjectResult NotImplemented() =>
        StatusCode(StatusCodes.Status501NotImplemented, new ApiError(
            ErrorCodes.NotImplemented,
            "This endpoint is not implemented yet."));
}
