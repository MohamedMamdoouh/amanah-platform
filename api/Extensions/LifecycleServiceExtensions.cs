using Amanah.Api.Options;
using Amanah.Api.Services.Jobs;
using Amanah.Api.Services.Lifecycle;

namespace Amanah.Api.Extensions;

public static class LifecycleServiceExtensions
{
    public static IServiceCollection AddLifecycleServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<LifecycleOptions>(configuration.GetSection(LifecycleOptions.SectionName));
        services.AddScoped<ReportLifecycleService>();
        services.AddScoped<AccountDeactivationService>();
        services.AddScoped<RetentionService>();
        services.AddScoped<OrphanedStorageCleanupService>();
        services.AddScoped<JobRunner>();
        services.AddHostedService<LifecycleJobsHostedService>();
        services.AddLifecycleJob<ListingExpiryWarningJob>();
        services.AddLifecycleJob<ListingAutoExpiryJob>();
        services.AddLifecycleJob<PendingClaimTimeoutJob>();
        services.AddLifecycleJob<RejectedReportCleanupJob>();
        services.AddLifecycleJob<ChatRetentionJob>();
        services.AddLifecycleJob<StorageDeletionOutboxCleanupJob>();
        services.AddLifecycleJob<OtpCleanupJob>();
        services.AddLifecycleJob<SessionCleanupJob>();
        services.AddLifecycleJob<OtpSmsOutboxCleanupJob>();
        services.AddLifecycleJob<OtpEmailOutboxCleanupJob>();
        services.AddLifecycleJob<AdminAlertEmailOutboxCleanupJob>();
        services.AddLifecycleJob<NotificationCleanupJob>();
        services.AddLifecycleJob<OrphanedStorageCleanupJob>();

        return services;
    }

    public static IServiceCollection AddLifecycleJob<TJob>(this IServiceCollection services)
        where TJob : class, ILifecycleJob
    {
        services.AddScoped<ILifecycleJob, TJob>();
        return services;
    }
}
