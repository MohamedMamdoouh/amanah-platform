using Amanah.Api.Data;
using Amanah.Api.Data.Entities;
using Amanah.Api.Options;
using Amanah.Api.Services.External;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Amanah.Api.Services.Auth;

public sealed class OtpSmsOutboxDispatcher(
    AppDbContext dbContext,
    ISmsSender smsSender,
    IDataProtectionProvider dataProtectionProvider,
    IOptions<OtpOptions> options,
    TimeProvider timeProvider,
    ILogger<OtpSmsOutboxDispatcher> logger)
{
    public async Task<bool> DispatchAsync(Guid outboxId, CancellationToken cancellationToken = default)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        await dbContext.Database.ExecuteSqlAsync(
            $"SELECT pg_advisory_xact_lock(hashtext({outboxId.ToString()}))",
            cancellationToken);

        var message = await dbContext.OtpSmsOutboxMessages
            .FirstOrDefaultAsync(entry => entry.Id == outboxId, cancellationToken);

        if (message is null)
        {
            logger.LogWarning("OTP SMS outbox message {OutboxId} was not found.", outboxId);
            await transaction.RollbackAsync(cancellationToken);
            return false;
        }

        if (message.Status == OtpSmsOutboxStatus.Sent)
        {
            await transaction.CommitAsync(cancellationToken);
            return true;
        }

        if (message.Status == OtpSmsOutboxStatus.Failed)
        {
            await transaction.RollbackAsync(cancellationToken);
            return false;
        }

        if (message.Status != OtpSmsOutboxStatus.Pending)
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

        string plainOtpCode = string.Empty;
        try
        {
            plainOtpCode = OtpSmsOutboxPayload.Unprotect(dataProtectionProvider, message.ProtectedPayload);
        }
        catch (Exception exception)
        {
            await MarkFailedAsync(message, $"Failed to decrypt outbox payload: {exception.Message}", cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return false;
        }

        try
        {
            await smsSender.SendOtpAsync(message.Phone, plainOtpCode, message.Id, CancellationToken.None);
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
        message.Status = OtpSmsOutboxStatus.Sent;
        message.ProcessedAt = now;
        message.LastError = null;
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return true;
    }

    private Task MarkAmbiguousAsync(
        OtpSmsOutboxMessage message,
        string error,
        CancellationToken cancellationToken)
    {
        message.Status = OtpSmsOutboxStatus.Pending;
        message.LastError = error;
        return dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task MarkFailedAsync(
        OtpSmsOutboxMessage message,
        string error,
        CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        message.Status = OtpSmsOutboxStatus.Failed;
        message.ProcessedAt = now;
        message.LastError = error;

        if (message.OtpCodeId is Guid otpCodeId)
        {
            await dbContext.OtpCodes
                .Where(otpCode => otpCode.Id == otpCodeId)
                .ExecuteDeleteAsync(cancellationToken);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
