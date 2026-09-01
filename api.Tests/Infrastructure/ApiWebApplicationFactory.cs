using Amanah.Api.Services.Catalog;
using Amanah.Api.Tests.Auth.Fakes;
using Amanah.Api.Tests.Catalog;
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
                ["Database:AutoMigrate"] = "false",
                ["Jwt:AccessTokenSigningKey"] = "test-access-signing-key-at-least-32-characters!",
                ["Jwt:HandoffTokenSigningKey"] = "test-handoff-signing-key-at-least-32-characters!",
                ["ADMIN_PHONE"] = "+201011111111",
                ["ADMIN_PASSWORD"] = "AdminPass123",
                ["RateLimit:Policies:auth-login:PermitLimit"] = "1000",
                ["RateLimit:Policies:auth-login:WindowSeconds"] = "3600",
            });
        });

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<ISmsSender>();
            services.RemoveAll<ICaptchaVerifier>();
            services.RemoveAll<ICategoryLoader>();
            services.RemoveAll<IGovernorateLoader>();
            services.AddSingleton<ISmsSender>(SmsSender);
            services.AddSingleton<ICaptchaVerifier>(CaptchaVerifier);
            services.AddScoped<CategoryLoader>();
            services.AddScoped<CountingCategoryLoader>(sp =>
                new CountingCategoryLoader(sp.GetRequiredService<CategoryLoader>()));
            services.AddScoped<ICategoryLoader>(sp => sp.GetRequiredService<CountingCategoryLoader>());
            services.AddScoped<IGovernorateLoader, GovernorateLoader>();
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
