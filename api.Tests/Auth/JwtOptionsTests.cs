using Amanah.Api.Extensions;
using Amanah.Api.Options;
using Amanah.Api.Services.Auth;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Amanah.Api.Tests.Auth;

public class JwtOptionsTests
{
    [Theory]
    [InlineData("")]
    [InlineData("too-short-to-be-an-hs256-key")]
    public async Task Host_start_rejects_signing_key_shorter_than_32_bytes(string signingKey)
    {
        using var host = CreateHost(signingKey);

        var exception = await Assert.ThrowsAsync<OptionsValidationException>(
            () => host.StartAsync());

        Assert.Contains("Jwt:SigningKey", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Host_start_accepts_32_byte_signing_key()
    {
        using var host = CreateHost(new string('k', 32));

        await host.StartAsync();
        await host.StopAsync();
    }

    [Fact]
    public void Issue_throws_when_signing_key_is_empty()
    {
        var service = new HandoffTokenService(
            Options.Create(new JwtOptions
            {
                SigningKey = string.Empty,
                HandoffTokenLifetimeMinutes = 15,
            }),
            TimeProvider.System);

        Assert.ThrowsAny<ArgumentException>(
            () => service.Issue("+201012345678", AuthTokenPurposes.Signup));
    }

    private static IHost CreateHost(string signingKey)
    {
        return new HostBuilder()
            .ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Jwt:SigningKey"] = signingKey,
                    ["Jwt:HandoffTokenLifetimeMinutes"] = "15",
                });
            })
            .ConfigureServices((context, services) =>
            {
                services.AddJwtOptions(context.Configuration);
            })
            .Build();
    }
}
