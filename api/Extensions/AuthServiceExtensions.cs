using System.Text;
using Amanah.Api.Options;
using Amanah.Api.Services.Auth;
using Amanah.Api.Services.External;
using Microsoft.Extensions.Options;

namespace Amanah.Api.Extensions;

public static class AuthServiceExtensions
{
    public static OptionsBuilder<JwtOptions> AddJwtOptions(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // HS256 rejects keys shorter than 32 bytes; fail at startup instead of
        // after a successful OTP verify has already consumed the code.
        return services.AddOptions<JwtOptions>()
            .Bind(configuration.GetSection(JwtOptions.SectionName))
            .Validate(
                static options => Encoding.UTF8.GetByteCount(options.SigningKey ?? string.Empty) >= 32,
                "Jwt:SigningKey must be at least 32 UTF-8 bytes for HMAC-SHA256.")
            .ValidateOnStart();
    }

    public static IServiceCollection AddAuthServices(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        services.AddSingleton(TimeProvider.System);
        services.Configure<OtpOptions>(configuration.GetSection(OtpOptions.SectionName));
        services.AddJwtOptions(configuration);
        services.Configure<TurnstileOptions>(configuration.GetSection(TurnstileOptions.SectionName));
        services.AddDataProtection();
        services.AddScoped<OtpSmsOutboxDispatcher>();
        services.AddScoped<HandoffTokenService>();
        services.AddScoped<OtpService>();
        services.AddHostedService<OtpSmsOutboxProcessor>();

        if (environment.IsDevelopment())
        {
            services.AddSingleton<ISmsSender, ConsoleSmsSender>();
            services.AddSingleton<ICaptchaVerifier, FakeCaptchaVerifier>();
        }
        else
        {
            services.AddHttpClient<ICaptchaVerifier, TurnstileCaptchaVerifier>();
            services.AddSingleton<ISmsSender, ConsoleSmsSender>();
        }

        return services;
    }
}
