using Amanah.Api.Models.Auth;
using Amanah.Api.Models.Errors;
using Amanah.Api.Services.Auth;
using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Amanah.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/auth")]
public sealed class AuthController(OtpService otpService) : ControllerBase
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
}
