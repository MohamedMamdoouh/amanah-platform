using Amanah.Api.Data;
using Amanah.Api.Data.Entities;
using Amanah.Api.Options;
using Amanah.Api.Services.External;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Amanah.Api.Services.Reports;

public sealed class AdminAlertEmailOutboxDispatcher(
    AppDbContext dbContext,
    IAdminAlertEmailSender adminAlertEmailSender,
    IOptions<EmailOptions> options,
    TimeProvider timeProvider,
    ILogger<AdminAlertEmailOutboxDispatcher> logger)
{
    public async Task<bool> DispatchAsync(Guid outboxId, CancellationToken cancellationToken = default)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        await dbContext.Database.ExecuteSqlAsync(
            $"SELECT pg_advisory_xact_lock(hashtext({outboxId.ToString()}))",
            cancellationToken);

        var message = await dbContext.AdminAlertEmailOutboxMessages
            .FirstOrDefaultAsync(entry => entry.Id == outboxId, cancellationToken);

        if (message is null)
        {
            logger.LogWarning("Admin alert email outbox message {OutboxId} was not found.", outboxId);
            await transaction.RollbackAsync(cancellationToken);
            return false;
        }

        if (message.Status == AdminAlertEmailOutboxStatus.Sent)
        {
            await transaction.CommitAsync(cancellationToken);
            return true;
        }

        if (message.Status == AdminAlertEmailOutboxStatus.Failed)
        {
            await transaction.RollbackAsync(cancellationToken);
            return false;
        }

        if (message.Status != AdminAlertEmailOutboxStatus.Pending)
        {
            await transaction.RollbackAsync(cancellationToken);
            return false;
        }

        if (message.AttemptCount >= options.Value.OutboxMaxAttempts)
        {
            await MarkFailedAsync(message, "Maximum dispatch attempts exceeded.", cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return false;
        }

        message.AttemptCount++;

        try
        {
            await adminAlertEmailSender.SendNewSubmissionAlertAsync(
                message.ReportId,
                message.ReportType,
                message.CategoryCode,
                CancellationToken.None);
        }
        catch (ResendApiException exception) when (exception.IsTransient)
        {
            await MarkAmbiguousAsync(message, exception.Message, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return false;
        }
        catch (ResendApiException exception)
        {
            await MarkFailedAsync(message, exception.Message, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return false;
        }
        catch (HttpRequestException exception)
        {
            await MarkFailedAsync(message, exception.Message, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return false;
        }
        catch (Exception exception) when (exception is TimeoutException or TaskCanceledException)
        {
            await MarkAmbiguousAsync(message, exception.Message, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return false;
        }

        var now = timeProvider.GetUtcNow();
        message.Status = AdminAlertEmailOutboxStatus.Sent;
        message.ProcessedAt = now;
        message.LastError = null;
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return true;
    }

    private Task MarkAmbiguousAsync(
        AdminAlertEmailOutboxMessage message,
        string error,
        CancellationToken cancellationToken)
    {
        message.Status = AdminAlertEmailOutboxStatus.Pending;
        message.LastError = error;
        return dbContext.SaveChangesAsync(cancellationToken);
    }

    private Task MarkFailedAsync(
        AdminAlertEmailOutboxMessage message,
        string error,
        CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        message.Status = AdminAlertEmailOutboxStatus.Failed;
        message.ProcessedAt = now;
        message.LastError = error;
        return dbContext.SaveChangesAsync(cancellationToken);
    }
}
