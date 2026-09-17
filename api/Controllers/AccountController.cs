using Amanah.Api.Auth;
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
    AccountDeletionService accountDeletionService,
    RefreshTokenCookieManager refreshTokenCookies) : ControllerBase
{
    [HttpGet("deletion-status")]
    [EndpointName(nameof(GetAccountDeletionStatus))]
    [EndpointSummary("Check whether account deletion is allowed and list blockers.")]
    [ProducesResponseType(typeof(AccountDeletionStatusResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetAccountDeletionStatus(CancellationToken cancellationToken)
    {
        User.TryGetUserId(out var userId);

        var result = await accountDeletionService.GetDeletionStatusAsync(userId, cancellationToken);
        return result.ToActionResult();
    }

    [HttpDelete]
    [EndpointName(nameof(DeleteAccount))]
    [EndpointSummary("Request self-serve account deletion.")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> DeleteAccount(CancellationToken cancellationToken)
    {
        User.TryGetUserId(out var userId);

        var result = await accountDeletionService.DeleteAccountAsync(userId, cancellationToken);
        if (result.IsSuccess)
        {
            refreshTokenCookies.Clear(Response);
        }

        return result.ToActionResult();
    }
}
