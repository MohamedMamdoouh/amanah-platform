using Amanah.Api.Options;
using Amanah.Api.Services.Auth;
using Amanah.Api.Services.External;

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
        services.Configure<TurnstileOptions>(configuration.GetSection(TurnstileOptions.SectionName));
        services.AddDataProtection();
        services.AddScoped<OtpSmsOutboxDispatcher>();
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
