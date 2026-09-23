using Amanah.Api.Auth;
using Amanah.Api.Services.Account;
using Amanah.Api.Services.Lifecycle;
using Amanah.Contracts.Requests.Account;
using Microsoft.AspNetCore.RateLimiting;
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
    AccountIdentifierService accountIdentifierService,
    RefreshTokenCookieManager refreshTokenCookies) : ControllerBase
{
    [HttpGet("identifiers")]
    [Authorize(Policy = AuthPolicies.ActiveAccount)]
    [EndpointName(nameof(GetAccountIdentifiers))]
    [EndpointSummary("Get linked phone and email identifiers for the current account.")]
    [ProducesResponseType(typeof(AccountIdentifiersResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetAccountIdentifiers(CancellationToken cancellationToken)
    {
        User.TryGetUserId(out var userId);

        var result = await accountIdentifierService.GetIdentifiersAsync(userId, cancellationToken);
        return result.ToActionResult();
    }

    [HttpPost("identifiers/otp/send")]
    [Authorize(Policy = AuthPolicies.ActiveAccount)]
    [EnableRateLimiting("otp-send")]
    [EndpointName(nameof(SendLinkIdentifierOtp))]
    [EndpointSummary("Send a one-time password to link a phone number or email to the current account.")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> SendLinkIdentifierOtp(
        [FromBody] SendLinkIdentifierOtpRequest request,
        CancellationToken cancellationToken)
    {
        User.TryGetUserId(out var userId);

        var result = await accountIdentifierService.SendLinkOtpAsync(
            userId,
            request.Channel,
            request.Identifier,
            request.CaptchaToken,
            cancellationToken);

        return result.ToActionResult();
    }

    [HttpPost("identifiers/otp/verify")]
    [Authorize(Policy = AuthPolicies.ActiveAccount)]
    [EndpointName(nameof(VerifyLinkIdentifierOtp))]
    [EndpointSummary("Verify a one-time password and link a phone number or email to the current account.")]
    [ProducesResponseType(typeof(AccountIdentifiersResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> VerifyLinkIdentifierOtp(
        [FromBody] VerifyLinkIdentifierOtpRequest request,
        CancellationToken cancellationToken)
    {
        User.TryGetUserId(out var userId);

        var result = await accountIdentifierService.VerifyLinkOtpAsync(
            userId,
            request.Channel,
            request.Identifier,
            request.Code,
            cancellationToken);

        return result.ToActionResult();
    }

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
