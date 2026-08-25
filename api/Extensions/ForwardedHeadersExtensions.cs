using Amanah.Api.Options;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Options;

namespace Amanah.Api.Extensions;

public static class ForwardedHeadersExtensions
{
    public static IServiceCollection AddForwardedHeaders(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<Options.ForwardedHeadersOptions>()
            .Bind(configuration.GetSection(Options.ForwardedHeadersOptions.SectionName));

        services.ConfigureOptions<ConfigureForwardedHeadersOptions>();
        return services;
    }
}

public sealed class ConfigureForwardedHeadersOptions(IOptions<Options.ForwardedHeadersOptions> settings)
    : IConfigureOptions<Microsoft.AspNetCore.Builder.ForwardedHeadersOptions>
{
    private readonly Options.ForwardedHeadersOptions _settings = settings.Value;

    public void Configure(Microsoft.AspNetCore.Builder.ForwardedHeadersOptions options)
    {
        options.ForwardedHeaders = ForwardedHeaders.XForwardedFor;
        options.ForwardLimit = 1;

        options.KnownIPNetworks.Clear();
        options.KnownProxies.Clear();

        foreach (var cidr in _settings.KnownNetworks)
        {
            options.KnownIPNetworks.Add(System.Net.IPNetwork.Parse(cidr));
        }
    }
}
