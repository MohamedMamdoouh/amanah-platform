using Amanah.Api.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Amanah.Api.Controllers;

[ApiController]
[AllowAnonymous]
public sealed class HealthController(AppDbContext dbContext) : ControllerBase
{
    [HttpGet("/health")]
    [EndpointSummary("Health check for deploy probes and keepalive cron.")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> Get(CancellationToken cancellationToken)
    {
        if (!await dbContext.Database.CanConnectAsync(cancellationToken))
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable);
        }

        await dbContext.Database.ExecuteSqlRawAsync("SELECT 1", cancellationToken);

        return Ok();
    }
}
