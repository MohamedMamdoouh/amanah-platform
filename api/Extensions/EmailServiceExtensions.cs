using Amanah.Api.Options;
using Amanah.Api.Services.External;
using Amanah.Api.Services.Reports;

namespace Amanah.Api.Extensions;

public static class EmailServiceExtensions
{
    public static IServiceCollection AddEmailServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<EmailOptions>(configuration.GetSection(EmailOptions.SectionName));
        services.AddScoped<AdminSubmissionAlertNotifier>();
        services.AddScoped<AdminAlertEmailOutboxDispatcher>();
        services.AddHostedService<AdminAlertEmailOutboxProcessor>();

        var emailOptions = configuration.GetSection(EmailOptions.SectionName).Get<EmailOptions>();
        if (emailOptions?.IsConfigured == true)
        {
            services.AddHttpClient<IAdminAlertEmailSender, ResendAdminAlertEmailSender>();
        }
        else
        {
            services.AddSingleton<IAdminAlertEmailSender, NullAdminAlertEmailSender>();
        }

        return services;
    }
}
