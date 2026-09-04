using Amanah.Api.Services.Reports;

namespace Amanah.Api.Extensions;

public static class ReportServiceExtensions
{
    public static IServiceCollection AddReportServices(this IServiceCollection services)
    {
        services.AddScoped<IReportQuotaService, ReportQuotaService>();
        services.AddScoped<ReportService>();
        services.AddScoped<ReportPhotoAttachService>();
        services.AddScoped<ReportCreateFormParser>();

        return services;
    }
}
