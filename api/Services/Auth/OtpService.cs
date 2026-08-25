using System.Security.Cryptography;
using Amanah.Api.Data;
using Amanah.Api.Data.Entities;
using Amanah.Api.Models.Auth;
using Amanah.Api.Models.Errors;
using Amanah.Api.Models.Results;
using Amanah.Api.Options;
using Amanah.Api.Services.External;
using Amanah.Api.Utilities;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Amanah.Api.Services.Auth;

public sealed class OtpService(
    AppDbContext dbContext,
    ICaptchaVerifier captchaVerifier,
    IDataProtectionProvider dataProtectionProvider,
    HandoffTokenService handoffTokenService,
    IOptions<OtpOptions> options,
    TimeProvider timeProvider)
{
    public async Task<Result> SendAsync(
        string phone,
        string captchaToken,
        CancellationToken cancellationToken = default)
    {
        // Reject input that cannot be normalized to E.164 (e.g. invalid Egyptian mobile).
        if (!PhoneNormalizer.TryNormalize(phone, out var normalizedPhone))
        {
            return ResultError.BadRequest(
                "The phone number format is not accepted.",
                ErrorCodes.InvalidPhone);
        }

        // Block automated abuse before any OTP work or DB writes.
        var captchaResult = await captchaVerifier.VerifyAsync(captchaToken, cancellationToken);
        if (!captchaResult.IsSuccess)
        {
            return captchaResult;
        }

        var now = timeProvider.GetUtcNow();
        var code = RandomNumberGenerator.GetInt32(100_000, 1_000_000).ToString();

        // Verification row: store only a hash; plaintext exists only in memory until outbox dispatch.
        var otpCode = new OtpCode
        {
            Phone = normalizedPhone,
            CodeHash = OtpHasher.Hash(code),
            ExpiresAt = now.AddMinutes(options.Value.CodeLifetimeMinutes),
            AttemptCount = 0,
            CreatedAt = now,
        };

        // Outbox row: encrypted payload for the background worker; Id becomes the SMS idempotency key.
        var outboxMessage = new OtpSmsOutboxMessage
        {
            OtpCode = otpCode,
            Phone = normalizedPhone,
            ProtectedPayload = OtpSmsOutboxPayload.Protect(dataProtectionProvider, code),
            Status = OtpSmsOutboxStatus.Pending,
            CreatedAt = now,
        };

        // Atomic enqueue: limits, supersede old codes, and insert new rows commit together or not at all.
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        // Serialize concurrent send requests for the same phone (limits + replace logic).
        await dbContext.Database.ExecuteSqlAsync(
            $"SELECT pg_advisory_xact_lock(hashtext({normalizedPhone}))",
            cancellationToken);

        // Cooldown / hourly / daily limits count prior Sent outbox rows for this phone.
        var limitsResult = await EnforceSendLimitsAsync(normalizedPhone, now, cancellationToken);
        if (!limitsResult.IsSuccess)
        {
            await transaction.RollbackAsync(cancellationToken);
            return limitsResult;
        }

        // A new send invalidates any previous code still waiting to be verified.
        await dbContext.OtpCodes
            .Where(existing => existing.Phone == normalizedPhone)
            .ExecuteDeleteAsync(cancellationToken);

        // If the user requests another code before the worker sends the previous one, mark that
        // older Pending outbox row Failed so the worker skips it and only dispatches this new code.
        await dbContext.OtpSmsOutboxMessages
            .Where(message => message.Phone == normalizedPhone
                && message.Status == OtpSmsOutboxStatus.Pending)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(message => message.Status, OtpSmsOutboxStatus.Failed)
                    .SetProperty(message => message.ProcessedAt, now),
                cancellationToken);

        dbContext.OtpCodes.Add(otpCode);
        dbContext.OtpSmsOutboxMessages.Add(outboxMessage);
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        // Caller gets 204 here; OtpSmsOutboxProcessor sends the SMS asynchronously.
        return Result.Ok();
    }

    public async Task<Result<VerifyOtpResponse>> VerifyAsync(
        string phone,
        string code,
        CancellationToken cancellationToken = default)
    {
        if (!PhoneNormalizer.TryNormalize(phone, out var normalizedPhone))
        {
            return ResultError.BadRequest(
                "The phone number format is not accepted.",
                ErrorCodes.InvalidPhone);
        }

        if (!OtpCodeNormalizer.TryNormalize(code, out var normalizedCode))
        {
            return ResultError.BadRequest(
                "The OTP code format is not accepted.",
                ErrorCodes.InvalidOtp);
        }

        var now = timeProvider.GetUtcNow();
        var maxAttempts = options.Value.MaxVerificationAttempts;

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        await dbContext.Database.ExecuteSqlAsync(
            $"SELECT pg_advisory_xact_lock(hashtext({normalizedPhone}))",
            cancellationToken);

        var otpCode = await dbContext.OtpCodes
            .Where(existing => existing.Phone == normalizedPhone)
            .OrderByDescending(existing => existing.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (otpCode is null)
        {
            await transaction.RollbackAsync(cancellationToken);
            return ResultError.BadRequest(
                "The OTP code has expired. Please request a new code.",
                ErrorCodes.OtpExpired);
        }

        if (otpCode.ExpiresAt < now)
        {
            dbContext.OtpCodes.Remove(otpCode);
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return ResultError.BadRequest(
                "The OTP code has expired. Please request a new code.",
                ErrorCodes.OtpExpired);
        }

        if (otpCode.AttemptCount >= maxAttempts)
        {
            await transaction.RollbackAsync(cancellationToken);
            return ResultError.BadRequest(
                "The OTP code is no longer valid. Please request a new code.",
                ErrorCodes.OtpVoid);
        }

        if (!OtpHasher.Verify(normalizedCode, otpCode.CodeHash))
        {
            otpCode.AttemptCount++;

            if (otpCode.AttemptCount >= maxAttempts)
            {
                dbContext.OtpCodes.Remove(otpCode);
                await dbContext.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return ResultError.BadRequest(
                    "The OTP code is no longer valid. Please request a new code.",
                    ErrorCodes.OtpVoid);
            }

            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return ResultError.BadRequest(
                "The OTP code is incorrect.",
                ErrorCodes.InvalidOtp);
        }

        dbContext.OtpCodes.Remove(otpCode);
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        var userExists = await dbContext.Users
            .AsNoTracking()
            .AnyAsync(user => user.NormalizedPhone == normalizedPhone, cancellationToken);

        if (userExists)
        {
            return new VerifyOtpResponse
            {
                Status = "existing_user",
                SignupToken = null,
                LoginToken = handoffTokenService.Issue(normalizedPhone, AuthTokenPurposes.Login),
            };
        }

        return new VerifyOtpResponse
        {
            Status = "new_user",
            SignupToken = handoffTokenService.Issue(normalizedPhone, AuthTokenPurposes.Signup),
            LoginToken = null,
        };
    }

    private async Task<Result> EnforceSendLimitsAsync(
        string normalizedPhone,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        // Count only successfully delivered SMS (Sent), not Pending/Failed.
        var sentTimes = await dbContext.OtpSmsOutboxMessages
            .AsNoTracking()
            .Where(message => message.Phone == normalizedPhone
                && message.Status == OtpSmsOutboxStatus.Sent
                && message.ProcessedAt != null)
            .OrderByDescending(message => message.ProcessedAt)
            .Select(message => message.ProcessedAt!.Value)
            .ToListAsync(cancellationToken);

        // First-ever send for this phone: no prior Sent rows, skip all limit checks.
        if (sentTimes.Count == 0)
        {
            return Result.Ok();
        }

        var otpOptions = options.Value;

        // Cooldown: block rapid resends until CooldownSeconds after the most recent successful SMS.
        var lastSentAt = sentTimes[0];
        var cooldownEndsAt = lastSentAt.AddSeconds(otpOptions.CooldownSeconds);

        if (cooldownEndsAt > now)
        {
            return RateLimitError(
                ErrorCodes.OtpCooldown,
                $"Please wait {(int)Math.Ceiling((cooldownEndsAt - now).TotalSeconds)} seconds before requesting a new code.",
                cooldownEndsAt,
                now);
        }

        // Hourly cap: rolling 60-minute window from now, not calendar hour.
        var hourlyWindowStart = now.AddHours(-1);
        var hourlySends = sentTimes.Count(sentAt => sentAt >= hourlyWindowStart);

        if (hourlySends >= otpOptions.HourlySendLimit)
        {
            // Retry when the oldest send in the window falls outside the last hour.
            var oldestInWindow = sentTimes
                .Where(sentAt => sentAt >= hourlyWindowStart)
                .MinBy(sentAt => sentAt);

            var hourlyRetryAt = oldestInWindow.AddHours(1);
            return RateLimitError(
                ErrorCodes.OtpHourlyLimit,
                "You have reached the hourly OTP send limit. Please try again later.",
                hourlyRetryAt,
                now);
        }

        // Daily cap: calendar day in Africa/Cairo (product timezone), not UTC midnight.
        var cairoDayStart = CairoTime.CairoDayStartUtc(now);
        var dailySends = sentTimes.Count(sentAt => sentAt >= cairoDayStart);

        if (dailySends >= otpOptions.DailySendLimit)
        {
            var nextCairoMidnight = cairoDayStart.AddDays(1);
            return RateLimitError(
                ErrorCodes.OtpDailyLimit,
                "You have reached the daily OTP send limit. Please try again tomorrow.",
                nextCairoMidnight,
                now);
        }

        return Result.Ok();
    }

    // 429 with Retry-After derived from when the blocked limit window ends.
    private static ResultError RateLimitError(
        string code,
        string message,
        DateTimeOffset retryAt,
        DateTimeOffset now)
    {
        var retryAfterSeconds = (int)Math.Ceiling((retryAt - now).TotalSeconds);
        return ResultError.TooManyRequests(message, retryAfterSeconds, code);
    }
}
