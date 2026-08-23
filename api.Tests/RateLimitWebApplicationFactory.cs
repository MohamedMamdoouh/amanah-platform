using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;

namespace Amanah.Api.Tests;

public class RateLimitWebApplicationFactory : ApiWebApplicationFactory
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Default"] = ConnectionString,
                ["RateLimit:PermitLimit"] = "2",
                ["RateLimit:WindowSeconds"] = "60",
            });
        });
    }
}
