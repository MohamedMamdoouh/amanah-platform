using Amanah.Api.Options;
using Amanah.Api.Http;
using Microsoft.AspNetCore.Http.Timeouts;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Http;

namespace Amanah.Api.Extensions;

public static class HttpTimeoutExtensions
{
    public static IServiceCollection AddHttpTimeouts(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var httpTimeoutSection = configuration.GetSection(HttpTimeoutOptions.SectionName);
        services.Configure<HttpTimeoutOptions>(httpTimeoutSection);

        var httpTimeouts = httpTimeoutSection.Get<HttpTimeoutOptions>() ?? new HttpTimeoutOptions();

        services.AddRequestTimeouts(options =>
        {
            options.DefaultPolicy = new RequestTimeoutPolicy
            {
                Timeout = TimeSpan.FromSeconds(httpTimeouts.IncomingRequestSeconds),
            };
        });

        services.TryAddTransient<OutgoingHttpTimeoutHandler>();

        // Apply the same outgoing timeout handler to all HttpClientFactory clients until a second
        // client with different timeout needs arises; then register per-client instead of ConfigureAll.
        services.ConfigureAll<HttpClientFactoryOptions>(options =>
        {
            options.HttpClientActions.Add(client =>
            {
                client.Timeout = Timeout.InfiniteTimeSpan;
            });

            options.HttpMessageHandlerBuilderActions.Add(builder =>
            {
                builder.AdditionalHandlers.Add(
                    builder.Services.GetRequiredService<OutgoingHttpTimeoutHandler>());
            });
        });

        return services;
    }

    public static WebApplication UseHttpTimeouts(this WebApplication app)
    {
        app.UseRequestTimeouts();
        return app;
    }
}
