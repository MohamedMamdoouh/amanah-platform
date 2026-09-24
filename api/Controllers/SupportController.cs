using Amanah.Api.Models.Errors;
using Amanah.Api.Services.Support;
using Amanah.Contracts.Errors;
using Amanah.Contracts.Requests.Support;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Amanah.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[AllowAnonymous]
[Route("api/v{version:apiVersion}/support")]
public sealed class SupportController(SupportService supportService) : ControllerBase
{
    [HttpPost("messages")]
    [EnableRateLimiting("support-message")]
    [EndpointName(nameof(SubmitSupportMessage))]
    [EndpointSummary("Send a support message to the Amanah team.")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> SubmitSupportMessage(
        [FromBody] SubmitSupportMessageRequest request,
        CancellationToken cancellationToken)
    {
        var result = await supportService.SubmitAsync(request, cancellationToken);
        return result.ToActionResult();
    }
}
