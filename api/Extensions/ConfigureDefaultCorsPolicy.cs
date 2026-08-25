using Amanah.Api.Options;
using Microsoft.Extensions.Options;

namespace Amanah.Api.Extensions;

public sealed class ConfigureDefaultCorsPolicy(IOptions<CorsOptions> corsOptions)
    : IConfigureOptions<Microsoft.AspNetCore.Cors.Infrastructure.CorsOptions>
{
    public void Configure(Microsoft.AspNetCore.Cors.Infrastructure.CorsOptions options)
    {
        var origins = corsOptions.Value.AllowedOrigins;

        options.AddDefaultPolicy(policy =>
        {
            if (origins.Length > 0)
            {
                policy.WithOrigins(origins);
            }

            policy.AllowAnyHeader()
                  .AllowAnyMethod();
        });
    }
}
