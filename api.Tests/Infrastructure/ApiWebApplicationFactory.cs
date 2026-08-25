using Amanah.Api.Tests.Auth.Fakes;
using Amanah.Api.Services.External;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Testcontainers.PostgreSql;

namespace Amanah.Api.Tests.Infrastructure;

public class ApiWebApplicationFactory : WebApplicationFactory<ApiAssemblyMarker>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:16").Build();

    public RecordingSmsSender SmsSender { get; } = new();

    public FakeCaptchaVerifier CaptchaVerifier { get; } = new();

    public string ConnectionString => _postgres.GetConnectionString();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Default"] = ConnectionString,
                ["RateLimit:Policies:otp-send:PermitLimit"] = "1000",
                ["RateLimit:Policies:otp-send:WindowSeconds"] = "3600",
                ["Otp:OutboxPollIntervalSeconds"] = "1",
            });
        });

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<ISmsSender>();
            services.RemoveAll<ICaptchaVerifier>();
            services.AddSingleton<ISmsSender>(SmsSender);
            services.AddSingleton<ICaptchaVerifier>(CaptchaVerifier);
        });
    }

    public async Task InitializeAsync() => await _postgres.StartAsync();

    public new async Task DisposeAsync()
    {
        SmsSender.SentMessages.Clear();
        SmsSender.ShouldThrow = false;
        SmsSender.ShouldTimeout = false;
        CaptchaVerifier.ShouldSucceed = true;

        await _postgres.DisposeAsync();
        await base.DisposeAsync();
    }
}
