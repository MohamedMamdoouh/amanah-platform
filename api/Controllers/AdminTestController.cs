using Amanah.Api.Auth;
using Amanah.Api.Models.Errors;
using Amanah.Api.Services.Jobs;
using Amanah.Contracts.Errors;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Amanah.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/admin/test")]
[Authorize(AuthPolicies.Admin)]
public sealed class AdminTestController(
    IJobRunner jobRunner,
    IHostEnvironment environment) : ControllerBase
{
    [HttpPost("run-job/{jobName}")]
    [EndpointName(nameof(RunLifecycleJob))]
    [EndpointSummary("Run a lifecycle job manually (non-production test harness).")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RunLifecycleJob(
        string jobName,
        CancellationToken cancellationToken)
    {
        if (environment.IsProduction())
        {
            return ResultError.NotFound("Not found.").ToActionResult();
        }

        var result = await jobRunner.RunAsync(jobName, cancellationToken);
        return result.ToActionResult();
    }
}
