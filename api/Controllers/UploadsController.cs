using Amanah.Api.Auth;
using Amanah.Api.Services.Uploads;
using Amanah.Contracts.Errors;
using Amanah.Contracts.Responses.Uploads;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Amanah.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/uploads")]
[Authorize]
public sealed class UploadsController(ReportPhotoPresignService presignService) : ControllerBase
{
    [HttpGet("report-photo/{id:guid}/url")]
    [EndpointName(nameof(GetReportPhotoUrl))]
    [EndpointSummary("Get a short-lived URL for a private report photo.")]
    [ProducesResponseType(typeof(ReportPhotoPresignResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetReportPhotoUrl(
        Guid id,
        CancellationToken cancellationToken)
    {
        User.TryGetUserId(out var userId);

        var result = await presignService.GetReportPhotoUrlAsync(
            id,
            userId,
            User.GetUserRole(),
            cancellationToken);
        return result.ToActionResult();
    }
}
