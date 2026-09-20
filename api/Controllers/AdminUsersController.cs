using Amanah.Api.Auth;
using Amanah.Api.Services.Enforcement;
using Amanah.Contracts.Errors;
using Amanah.Contracts.Requests.Admin;
using Amanah.Contracts.Responses.Admin;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Amanah.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/admin/users")]
[Authorize(AuthPolicies.Admin)]
public sealed class AdminUsersController(UserEnforcementService userEnforcementService) : ControllerBase
{
    [HttpPost("{id:guid}/ban")]
    [EndpointName(nameof(BanUser))]
    [EndpointSummary("Ban a user and run lifecycle cleanup.")]
    [ProducesResponseType(typeof(BanUserResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> BanUser(
        Guid id,
        [FromBody] BanUserRequest request,
        CancellationToken cancellationToken)
    {
        User.TryGetUserId(out var adminId);

        var result = await userEnforcementService.BanAsync(id, adminId, request, cancellationToken);
        return result.ToActionResult();
    }

    [HttpPost("{id:guid}/unban")]
    [EndpointName(nameof(UnbanUser))]
    [EndpointSummary("Unban a user without restoring withdrawn or cancelled content.")]
    [ProducesResponseType(typeof(UnbanUserResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> UnbanUser(Guid id, CancellationToken cancellationToken)
    {
        User.TryGetUserId(out var adminId);

        var result = await userEnforcementService.UnbanAsync(id, adminId, cancellationToken);
        return result.ToActionResult();
    }
}
