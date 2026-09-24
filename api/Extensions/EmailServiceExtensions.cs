using Amanah.Api.Options;
using Amanah.Api.Services.External;
using Amanah.Api.Services.Reports;

namespace Amanah.Api.Extensions;

public static class EmailServiceExtensions
{
    public static IServiceCollection AddEmailServices(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        services.Configure<EmailOptions>(configuration.GetSection(EmailOptions.SectionName));
        services.AddScoped<AdminAlertEmailOutboxDispatcher>();
        services.AddHostedService<AdminAlertEmailOutboxProcessor>();

        var emailOptions = configuration.GetSection(EmailOptions.SectionName).Get<EmailOptions>();
        if (emailOptions?.IsConfigured == true)
        {
            services.AddHttpClient<IAdminAlertEmailSender, BrevoAdminAlertEmailSender>();
        }
        else
        {
            services.AddSingleton<IAdminAlertEmailSender, NullAdminAlertEmailSender>();
        }

        if (environment.IsDevelopment())
        {
            services.AddSingleton<ISupportEmailSender, ConsoleSupportEmailSender>();
        }
        else if (emailOptions?.IsConfigured == true)
        {
            services.AddHttpClient<ISupportEmailSender, BrevoSupportEmailSender>();
        }
        else
        {
            services.AddSingleton<ISupportEmailSender, NullSupportEmailSender>();
        }

        return services;
    }
}
