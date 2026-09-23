using Amanah.Api.Services.Lifecycle;

namespace Amanah.Api.Services.Jobs;

public sealed class OtpCleanupJob(
    RetentionService retentionService,
    ILogger<OtpCleanupJob> logger) : ILifecycleJob
{
    public string Name => "OtpCleanup";

    public async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        var deletedCount = await retentionService.ProcessOtpCleanupAsync(cancellationToken);

        logger.LogInformation(
            "OTP cleanup job removed {DeletedCount} expired OTP code row(s).",
            deletedCount);
    }
}

public sealed class SessionCleanupJob(
    RetentionService retentionService,
    ILogger<SessionCleanupJob> logger) : ILifecycleJob
{
    public string Name => "SessionCleanup";

    public async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        var deletedCount = await retentionService.ProcessSessionCleanupAsync(cancellationToken);

        logger.LogInformation(
            "Session cleanup job removed {DeletedCount} expired refresh token row(s).",
            deletedCount);
    }
}

public sealed class OtpSmsOutboxCleanupJob(
    RetentionService retentionService,
    ILogger<OtpSmsOutboxCleanupJob> logger) : ILifecycleJob
{
    public string Name => "OtpSmsOutboxCleanup";

    public async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        var deletedCount = await retentionService.ProcessOtpSmsOutboxCleanupAsync(cancellationToken);

        logger.LogInformation(
            "OTP SMS outbox cleanup job removed {DeletedCount} processed row(s).",
            deletedCount);
    }
}

public sealed class OtpEmailOutboxCleanupJob(
    RetentionService retentionService,
    ILogger<OtpEmailOutboxCleanupJob> logger) : ILifecycleJob
{
    public string Name => "OtpEmailOutboxCleanup";

    public async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        var deletedCount = await retentionService.ProcessOtpEmailOutboxCleanupAsync(cancellationToken);

        logger.LogInformation(
            "OTP email outbox cleanup job removed {DeletedCount} processed row(s).",
            deletedCount);
    }
}

public sealed class AdminAlertEmailOutboxCleanupJob(
    RetentionService retentionService,
    ILogger<AdminAlertEmailOutboxCleanupJob> logger) : ILifecycleJob
{
    public string Name => "AdminAlertEmailOutboxCleanup";

    public async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        var deletedCount = await retentionService.ProcessAdminAlertEmailOutboxCleanupAsync(cancellationToken);

        logger.LogInformation(
            "Admin alert email outbox cleanup job removed {DeletedCount} processed row(s).",
            deletedCount);
    }
}

public sealed class NotificationCleanupJob(
    RetentionService retentionService,
    ILogger<NotificationCleanupJob> logger) : ILifecycleJob
{
    public string Name => "NotificationCleanup";

    public async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        var deletedCount = await retentionService.ProcessNotificationCleanupAsync(cancellationToken);

        logger.LogInformation(
            "Notification cleanup job removed {DeletedCount} notification row(s).",
            deletedCount);
    }
}
