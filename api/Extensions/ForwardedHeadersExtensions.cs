using Amanah.Api.Configuration;using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Options;

namespace Amanah.Api.Extensions;

public static class ForwardedHeadersExtensions
{
    public static IServiceCollection AddForwardedHeaders(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<ForwardedHeadersSettings>()
            .Bind(configuration.GetSection(ForwardedHeadersSettings.SectionName));

        services.ConfigureOptions<ConfigureForwardedHeadersOptions>();
        return services;
    }
}

public sealed class ConfigureForwardedHeadersOptions(IOptions<ForwardedHeadersSettings> settings)
    : IConfigureOptions<ForwardedHeadersOptions>
{
    private readonly ForwardedHeadersSettings _settings = settings.Value;

    public void Configure(ForwardedHeadersOptions options)
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
