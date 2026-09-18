using Amanah.Api.Auth;
using Amanah.Api.Models.Errors;
using Amanah.Api.Services.Lifecycle;
using Amanah.Contracts.Errors;
using Amanah.Contracts.Responses.Account;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Amanah.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/account")]
[Authorize]
public sealed class AccountController(
    AccountDeactivationService accountDeactivationService,
    RefreshTokenCookieManager refreshTokenCookies) : ControllerBase
{
    [HttpGet("deactivation-status")]
    [Authorize(Policy = AuthPolicies.ActiveAccount)]
    [EndpointName(nameof(GetAccountDeactivationStatus))]
    [EndpointSummary("Check whether account deactivation is allowed and list blockers.")]
    [ProducesResponseType(typeof(AccountDeactivationStatusResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetAccountDeactivationStatus(CancellationToken cancellationToken)
    {
        User.TryGetUserId(out var userId);

        var result = await accountDeactivationService.GetDeactivationStatusAsync(userId, cancellationToken);
        return result.ToActionResult();
    }

    [HttpPost("deactivate")]
    [Authorize(Policy = AuthPolicies.ActiveAccount)]
    [EndpointName(nameof(DeactivateAccount))]
    [EndpointSummary("Deactivate the current user's account.")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> DeactivateAccount(CancellationToken cancellationToken)
    {
        User.TryGetUserId(out var userId);

        var result = await accountDeactivationService.DeactivateAccountAsync(userId, cancellationToken);
        if (result.IsSuccess)
        {
            refreshTokenCookies.Clear(Response);
        }

        return result.ToActionResult();
    }

    [HttpPost("reactivate")]
    [EndpointName(nameof(ReactivateAccount))]
    [EndpointSummary("Reactivate a deactivated account.")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ReactivateAccount(CancellationToken cancellationToken)
    {
        User.TryGetUserId(out var userId);

        var result = await accountDeactivationService.ReactivateAccountAsync(userId, cancellationToken);
        return result.ToActionResult();
    }
}
