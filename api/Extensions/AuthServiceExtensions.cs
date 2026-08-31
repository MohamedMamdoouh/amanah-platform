using Amanah.Api.Auth;
using Amanah.Api.Options;
using Amanah.Api.Services.Auth;
using Amanah.Api.Services.External;
using Microsoft.Extensions.Options;
using UniSdk;

namespace Amanah.Api.Extensions;

public static class AuthServiceExtensions
{
    public static IServiceCollection AddAuthServices(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        services.AddSingleton(TimeProvider.System);
        services.Configure<OtpOptions>(configuration.GetSection(OtpOptions.SectionName));

        services.AddOptions<JwtOptions>()
            .Bind(configuration.GetSection(JwtOptions.SectionName))
            .Validate(
                options => !string.IsNullOrWhiteSpace(options.AccessTokenSigningKey),
                $"{JwtOptions.SectionName}:AccessTokenSigningKey is required.")
            .Validate(
                options => options.AccessTokenSigningKey.Length >= 32,
                $"{JwtOptions.SectionName}:AccessTokenSigningKey must be at least 32 characters.")
            .Validate(
                options => !string.IsNullOrWhiteSpace(options.HandoffTokenSigningKey),
                $"{JwtOptions.SectionName}:HandoffTokenSigningKey is required.")
            .Validate(
                options => options.HandoffTokenSigningKey.Length >= 32,
                $"{JwtOptions.SectionName}:HandoffTokenSigningKey must be at least 32 characters.")
            .ValidateOnStart();

        services.AddOptions<TurnstileOptions>()
            .Bind(configuration.GetSection(TurnstileOptions.SectionName))
            .Validate(
                options => environment.IsDevelopment() || !string.IsNullOrWhiteSpace(options.SecretKey),
                $"{TurnstileOptions.SectionName}:SecretKey is required outside Development.")
            .ValidateOnStart();

        services.AddOptions<SmsOptions>()
            .Bind(configuration.GetSection(SmsOptions.SectionName))
            .Validate(
                options => environment.IsDevelopment() || options.IsConfigured,
                $"{SmsOptions.SectionName} requires ApiKey outside Development.")
            .ValidateOnStart();

        services.AddDataProtection();
        services.AddSingleton<RefreshTokenCookieManager>();
        services.AddScoped<OtpSmsOutboxDispatcher>();
        services.AddScoped<HandoffTokenService>();
        services.AddScoped<TokenService>();
        services.AddScoped<AuthService>();
        services.AddScoped<OtpService>();
        services.AddHostedService<OtpSmsOutboxProcessor>();

        if (environment.IsDevelopment())
        {
            services.AddSingleton<ISmsSender, ConsoleSmsSender>();
            services.AddSingleton<ICaptchaVerifier, FakeCaptchaVerifier>();
        }
        else
        {
            services.AddSingleton(sp =>
            {
                var apiKey = sp.GetRequiredService<IOptions<SmsOptions>>().Value.ApiKey!;
                return new UniClient(apiKey);
            });
            services.AddSingleton<IUnimtxClient, UnimtxSdkClient>();
            services.AddHttpClient<ICaptchaVerifier, TurnstileCaptchaVerifier>();
            services.AddSingleton<ISmsSender, UnimtxSmsSender>();
        }

        return services;
    }
}
