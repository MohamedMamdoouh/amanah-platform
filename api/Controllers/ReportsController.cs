using Amanah.Api.Auth;
using Amanah.Api.Services.Reports;
using Amanah.Contracts.Errors;
using Amanah.Contracts.Requests.Reports;
using Amanah.Contracts.Responses.Reports;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Amanah.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/reports")]
[Authorize]
public sealed class ReportsController(
    ReportService reportService,
    ReportCreateFormParser createFormParser,
    ReportUpdateFormParser updateFormParser) : ControllerBase
{
    [HttpPost]
    [Consumes("multipart/form-data")]
    [EnableRateLimiting("photo-upload")]
    [EndpointName(nameof(CreateReport))]
    [EndpointSummary("Submit a lost or found report.")]
    [ProducesResponseType(typeof(CreateReportResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> CreateReport(CancellationToken cancellationToken)
    {
        User.TryGetUserId(out var userId);

        var parsed = await createFormParser.ParseAsync(Request, cancellationToken);
        if (!parsed.IsSuccess)
        {
            return parsed.Error!.ToActionResult();
        }

        var result = await reportService.CreateAsync(
            userId,
            parsed.Value!.Request,
            parsed.Value.Photos,
            cancellationToken);
        return result.ToActionResult();
    }

    [HttpGet("mine")]
    [EndpointName(nameof(GetMyReports))]
    [EndpointSummary("List the authenticated reporter's reports.")]
    [ProducesResponseType(typeof(ReportListResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetMyReports(
        [FromQuery] string? status,
        CancellationToken cancellationToken)
    {
        User.TryGetUserId(out var userId);

        var result = await reportService.GetMineAsync(userId, status, cancellationToken);
        return result.ToActionResult();
    }

    [HttpGet("{id:guid}")]
    [EndpointName(nameof(GetReport))]
    [EndpointSummary("Get report detail for the reporter or admin.")]
    [ProducesResponseType(typeof(ReportDetailResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetReport(
        Guid id,
        CancellationToken cancellationToken)
    {
        User.TryGetUserId(out var userId);

        var result = await reportService.GetByIdAsync(id, userId, User.GetUserRole(), cancellationToken);
        return result.ToActionResult();
    }

    [HttpPut("{id:guid}")]
    [Consumes("multipart/form-data")]
    [EnableRateLimiting("photo-upload")]
    [EndpointName(nameof(UpdateReport))]
    [EndpointSummary("Edit a rejected report.")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> UpdateReport(
        Guid id,
        CancellationToken cancellationToken)
    {
        User.TryGetUserId(out var userId);

        var parsed = await updateFormParser.ParseAsync(Request, cancellationToken);
        if (!parsed.IsSuccess)
        {
            return parsed.Error!.ToActionResult();
        }

        var result = await reportService.UpdateAsync(
            id,
            userId,
            parsed.Value!.Request,
            parsed.Value.Photos,
            cancellationToken);
        return result.ToActionResult();
    }

    [HttpPost("{id:guid}/resubmit")]
    [EndpointName(nameof(ResubmitReport))]
    [EndpointSummary("Resubmit a rejected report for moderation.")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ResubmitReport(
        Guid id,
        CancellationToken cancellationToken)
    {
        User.TryGetUserId(out var userId);

        var result = await reportService.ResubmitAsync(id, userId, cancellationToken);
        return result.ToActionResult();
    }

    [HttpPost("{id:guid}/withdraw")]
    [EndpointName(nameof(WithdrawReport))]
    [EndpointSummary("Withdraw a pending report.")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> WithdrawReport(
        Guid id,
        [FromBody] WithdrawReportRequest request,
        CancellationToken cancellationToken)
    {
        User.TryGetUserId(out var userId);

        var result = await reportService.WithdrawAsync(id, userId, request, cancellationToken);
        return result.ToActionResult();
    }
}
