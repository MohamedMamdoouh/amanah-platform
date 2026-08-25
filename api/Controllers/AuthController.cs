using System.Security.Claims;
using Amanah.Api.Auth;
using Amanah.Api.Models.Auth;
using Amanah.Api.Models.Errors;
using Amanah.Api.Services.Auth;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Amanah.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/auth")]
public sealed class AuthController(
    OtpService otpService,
    AuthService authService) : ControllerBase
{
    [HttpPost("otp/send")]
    [EnableRateLimiting("otp-send")]
    [EndpointName(nameof(SendOtp))]
    [EndpointSummary("Send a one-time password to an Egyptian mobile number.")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> SendOtp(
        [FromBody] SendOtpRequest request,
        CancellationToken cancellationToken)
    {
        var result = await otpService.SendAsync(
            request.Phone,
            request.CaptchaToken,
            cancellationToken);

        return result.ToActionResult();
    }

    [HttpPost("otp/verify")]
    [EndpointName(nameof(VerifyOtp))]
    [EndpointSummary("Verify a one-time password and distinguish new vs returning users.")]
    [ProducesResponseType(typeof(VerifyOtpResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> VerifyOtp(
        [FromBody] VerifyOtpRequest request,
        CancellationToken cancellationToken)
    {
        var result = await otpService.VerifyAsync(
            request.Phone,
            request.Code,
            cancellationToken);

        return result.ToActionResult();
    }

    [HttpPost("register")]
    [EndpointName(nameof(Register))]
    [EndpointSummary("Create an account after OTP verification.")]
    [ProducesResponseType(typeof(AuthSessionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Register(
        [FromBody] RegisterRequest request,
        CancellationToken cancellationToken)
    {
        var result = await authService.RegisterAsync(request, cancellationToken);
        return result.ToActionResult();
    }

    [HttpPost("login")]
    [EndpointName(nameof(Login))]
    [EndpointSummary("Sign in a returning user after OTP verification.")]
    [ProducesResponseType(typeof(AuthSessionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Login(
        [FromBody] LoginRequest request,
        CancellationToken cancellationToken)
    {
        var result = await authService.LoginAsync(request, cancellationToken);
        return result.ToActionResult();
    }

    [HttpPost("refresh")]
    [EndpointName(nameof(Refresh))]
    [EndpointSummary("Rotate refresh token and issue a new access token.")]
    [ProducesResponseType(typeof(AuthSessionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Refresh(
        [FromBody] RefreshRequest request,
        CancellationToken cancellationToken)
    {
        var result = await authService.RefreshAsync(request.RefreshToken, cancellationToken);
        return result.ToActionResult();
    }

    [HttpPost("logout")]
    [Authorize]
    [EndpointName(nameof(Logout))]
    [EndpointSummary("Revoke the current refresh token.")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Logout(
        [FromBody] LogoutRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId))
        {
            return ResultError.Unauthorized(
                "Authentication required.",
                ErrorCodes.Unauthorized).ToActionResult();
        }

        var result = await authService.LogoutAsync(userId, request.RefreshToken, cancellationToken);
        return result.ToActionResult();
    }

    [HttpPost("logout-everywhere")]
    [Authorize]
    [EndpointName(nameof(LogoutEverywhere))]
    [EndpointSummary("Revoke all refresh tokens for the current user.")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> LogoutEverywhere(CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId))
        {
            return ResultError.Unauthorized(
                "Authentication required.",
                ErrorCodes.Unauthorized).ToActionResult();
        }

        var result = await authService.LogoutEverywhereAsync(userId, cancellationToken);
        return result.ToActionResult();
    }

    [HttpGet("me")]
    [Authorize]
    [EndpointName(nameof(GetMe))]
    [EndpointSummary("Get the current user profile.")]
    [ProducesResponseType(typeof(UserProfileResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetMe(CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId))
        {
            return ResultError.Unauthorized(
                "Authentication required.",
                ErrorCodes.Unauthorized).ToActionResult();
        }

        var result = await authService.GetMeAsync(userId, cancellationToken);
        return result.ToActionResult();
    }

    private bool TryGetUserId(out Guid userId)
    {
        var sub = User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue(AuthClaimTypes.Sub);

        if (sub is not null && Guid.TryParse(sub, out userId))
        {
            return true;
        }

        userId = default;
        return false;
    }
}
