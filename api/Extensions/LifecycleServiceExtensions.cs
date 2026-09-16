using Amanah.Api.Options;
using Amanah.Api.Services.Jobs;

namespace Amanah.Api.Extensions;

public static class LifecycleServiceExtensions
{
    public static IServiceCollection AddLifecycleServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<LifecycleOptions>(configuration.GetSection(LifecycleOptions.SectionName));
        services.AddScoped<IJobRunner, JobRunner>();
        services.AddHostedService<LifecycleJobsHostedService>();

        return services;
    }

    public static IServiceCollection AddLifecycleJob<TJob>(this IServiceCollection services)
        where TJob : class, ILifecycleJob
    {
        services.AddScoped<ILifecycleJob, TJob>();
        return services;
    }
}
