using System.Security.Cryptography;
using Amanah.Api.Data;
using Amanah.Api.Data.Entities;
using Amanah.Contracts.Responses.Auth;
using Amanah.Contracts.Errors;
using Amanah.Api.Models.Errors;
using Amanah.Api.Options;
using Amanah.Api.Services.External;
using Amanah.Api.Utilities.Common;
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
    IOptions<EmailOptions> emailOptions,
    IHostEnvironment hostEnvironment,
    TimeProvider timeProvider)
{
    private const string InvalidIdentifierMessage =
        "The phone number or email format is not accepted.";

    public async Task<Result> SendAsync(
        string channel,
        string identifier,
        string captchaToken,
        string purpose,
        CancellationToken cancellationToken = default)
    {
        if (!AuthIdentifierNormalizer.TryResolve(channel, identifier, out var authIdentifier))
        {
            return ResultError.BadRequest(
                InvalidIdentifierMessage,
                ErrorCodes.FieldIdentifierInvalid);
        }

        if (authIdentifier.Channel == AuthIdentifierChannel.Email
            && !hostEnvironment.IsDevelopment()
            && !emailOptions.Value.IsConfigured)
        {
            return ResultError.ServiceUnavailable(
                "Email verification is temporarily unavailable. Try again later or use your phone number.",
                ErrorCodes.EmailUnavailable);
        }

        var userExists = await UserIdentifierQueries.ForIdentifier(
                dbContext.Users.AsNoTracking(),
                authIdentifier)
            .AnyAsync(cancellationToken);

        if (purpose == OtpPurposes.Signup && userExists)
        {
            return ResultError.Conflict(
                AccountExistsMessage(authIdentifier),
                ErrorCodes.AccountExists);
        }

        if (purpose == OtpPurposes.PasswordReset && !userExists)
        {
            return Result.Ok();
        }

        var captchaResult = await captchaVerifier.VerifyAsync(captchaToken, cancellationToken);
        if (!captchaResult.IsSuccess)
        {
            return captchaResult;
        }

        return await EnqueueOtpAsync(authIdentifier, cancellationToken);
    }

    public async Task<Result<VerifyOtpResponse>> VerifyAsync(
        string channel,
        string identifier,
        string code,
        string purpose,
        CancellationToken cancellationToken = default)
    {
        if (!AuthIdentifierNormalizer.TryResolve(channel, identifier, out var authIdentifier))
        {
            return ResultError.BadRequest(
                InvalidIdentifierMessage,
                ErrorCodes.FieldIdentifierInvalid);
        }

        if (!OtpCodeNormalizer.TryNormalize(code, out var normalizedCode))
        {
            return ResultError.BadRequest(
                "The OTP code format is not accepted.",
                ErrorCodes.InvalidOtp);
        }

        var now = timeProvider.GetUtcNow();
        var maxAttempts = options.Value.MaxVerificationAttempts;
        var lockKey = $"{authIdentifier.Channel}:{authIdentifier.NormalizedValue}";

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        await dbContext.Database.ExecuteSqlAsync(
            $"SELECT pg_advisory_xact_lock(hashtext({lockKey}))",
            cancellationToken);

        var otpCode = await dbContext.OtpCodes
            .Where(existing =>
                existing.Destination == authIdentifier.NormalizedValue
                && existing.Channel == authIdentifier.Channel)
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

        var userExists = await UserIdentifierQueries.ForIdentifier(
                dbContext.Users.AsNoTracking(),
                authIdentifier)
            .AnyAsync(cancellationToken);

        if (purpose == OtpPurposes.Signup)
        {
            if (userExists)
            {
                return ResultError.Conflict(
                    AccountExistsMessage(authIdentifier),
                    ErrorCodes.AccountExists);
            }

            return new VerifyOtpResponse
            {
                Status = VerifyOtpStatus.SignupReady,
                SignupToken = handoffTokenService.Issue(authIdentifier, AuthTokenPurposes.Signup),
                ResetToken = null,
            };
        }

        if (!userExists)
        {
            return ResultError.BadRequest(
                "The OTP code has expired. Please request a new code.",
                ErrorCodes.OtpExpired);
        }

        return new VerifyOtpResponse
        {
            Status = VerifyOtpStatus.ResetReady,
            SignupToken = null,
            ResetToken = handoffTokenService.Issue(authIdentifier, AuthTokenPurposes.Reset),
        };
    }

    internal async Task<Result> SendForLinkAsync(
        User user,
        string channel,
        string identifier,
        string captchaToken,
        CancellationToken cancellationToken = default)
    {
        if (!AuthIdentifierNormalizer.TryResolve(channel, identifier, out var authIdentifier))
        {
            return ResultError.BadRequest(
                InvalidIdentifierMessage,
                ErrorCodes.FieldIdentifierInvalid);
        }

        if (authIdentifier.Channel == AuthIdentifierChannel.Phone && user.NormalizedPhone is not null)
        {
            return ResultError.Conflict(
                "A phone number is already linked to this account.",
                ErrorCodes.Conflict);
        }

        if (authIdentifier.Channel == AuthIdentifierChannel.Email && user.NormalizedEmail is not null)
        {
            return ResultError.Conflict(
                "An email address is already linked to this account.",
                ErrorCodes.Conflict);
        }

        if (authIdentifier.Channel == AuthIdentifierChannel.Email
            && !hostEnvironment.IsDevelopment()
            && !emailOptions.Value.IsConfigured)
        {
            return ResultError.ServiceUnavailable(
                "Email verification is temporarily unavailable. Try again later.",
                ErrorCodes.EmailUnavailable);
        }

        if (await UserIdentifierQueries.ForIdentifier(
                    dbContext.Users.AsNoTracking(),
                    authIdentifier)
                .AnyAsync(cancellationToken))
        {
            return ResultError.Conflict(
                IdentifierTakenMessage(authIdentifier),
                ErrorCodes.Conflict);
        }

        var captchaResult = await captchaVerifier.VerifyAsync(captchaToken, cancellationToken);
        if (!captchaResult.IsSuccess)
        {
            return captchaResult;
        }

        return await EnqueueOtpAsync(authIdentifier, cancellationToken);
    }

    internal async Task<Result> VerifyForLinkAsync(
        User user,
        string channel,
        string identifier,
        string code,
        CancellationToken cancellationToken = default)
    {
        if (!AuthIdentifierNormalizer.TryResolve(channel, identifier, out var authIdentifier))
        {
            return ResultError.BadRequest(
                InvalidIdentifierMessage,
                ErrorCodes.FieldIdentifierInvalid);
        }

        if (authIdentifier.Channel == AuthIdentifierChannel.Phone && user.NormalizedPhone is not null)
        {
            return ResultError.Conflict(
                "A phone number is already linked to this account.",
                ErrorCodes.Conflict);
        }

        if (authIdentifier.Channel == AuthIdentifierChannel.Email && user.NormalizedEmail is not null)
        {
            return ResultError.Conflict(
                "An email address is already linked to this account.",
                ErrorCodes.Conflict);
        }

        if (!OtpCodeNormalizer.TryNormalize(code, out var normalizedCode))
        {
            return ResultError.BadRequest(
                "The OTP code format is not accepted.",
                ErrorCodes.InvalidOtp);
        }

        var now = timeProvider.GetUtcNow();
        var maxAttempts = options.Value.MaxVerificationAttempts;
        var lockKey = $"{authIdentifier.Channel}:{authIdentifier.NormalizedValue}";

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        await dbContext.Database.ExecuteSqlAsync(
            $"SELECT pg_advisory_xact_lock(hashtext({lockKey}))",
            cancellationToken);

        var otpCode = await dbContext.OtpCodes
            .Where(existing =>
                existing.Destination == authIdentifier.NormalizedValue
                && existing.Channel == authIdentifier.Channel)
            .OrderByDescending(existing => existing.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (otpCode is null || otpCode.ExpiresAt < now || otpCode.AttemptCount >= maxAttempts)
        {
            await transaction.RollbackAsync(cancellationToken);
            return ResultError.BadRequest(
                "The OTP code has expired. Please request a new code.",
                ErrorCodes.OtpExpired);
        }

        if (!OtpHasher.Verify(normalizedCode, otpCode.CodeHash))
        {
            otpCode.AttemptCount++;
            if (otpCode.AttemptCount >= maxAttempts)
            {
                dbContext.OtpCodes.Remove(otpCode);
            }

            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return ResultError.BadRequest(
                otpCode.AttemptCount >= maxAttempts
                    ? "The OTP code is no longer valid. Please request a new code."
                    : "The OTP code is incorrect.",
                otpCode.AttemptCount >= maxAttempts ? ErrorCodes.OtpVoid : ErrorCodes.InvalidOtp);
        }

        if (await UserIdentifierQueries.ForIdentifier(
                    dbContext.Users.AsNoTracking(),
                    authIdentifier)
                .AnyAsync(cancellationToken))
        {
            await transaction.RollbackAsync(cancellationToken);
            return ResultError.Conflict(
                IdentifierTakenMessage(authIdentifier),
                ErrorCodes.Conflict);
        }

        dbContext.OtpCodes.Remove(otpCode);

        if (authIdentifier.Channel == AuthIdentifierChannel.Phone)
        {
            user.NormalizedPhone = authIdentifier.NormalizedValue;
        }
        else
        {
            user.NormalizedEmail = authIdentifier.NormalizedValue;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Result.Ok();
    }

    private async Task<Result> EnqueueOtpAsync(
        AuthIdentifier authIdentifier,
        CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var code = RandomNumberGenerator.GetInt32(100_000, 1_000_000).ToString();
        var destination = authIdentifier.NormalizedValue;

        var otpCode = new OtpCode
        {
            Destination = destination,
            Channel = authIdentifier.Channel,
            CodeHash = OtpHasher.Hash(code),
            ExpiresAt = now.AddMinutes(options.Value.CodeLifetimeMinutes),
            AttemptCount = 0,
            CreatedAt = now,
        };

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        var lockKey = $"{authIdentifier.Channel}:{destination}";
        await dbContext.Database.ExecuteSqlAsync(
            $"SELECT pg_advisory_xact_lock(hashtext({lockKey}))",
            cancellationToken);

        var limitsResult = await EnforceSendLimitsAsync(authIdentifier, now, cancellationToken);
        if (!limitsResult.IsSuccess)
        {
            await transaction.RollbackAsync(cancellationToken);
            return limitsResult;
        }

        await dbContext.OtpCodes
            .Where(existing =>
                existing.Destination == destination
                && existing.Channel == authIdentifier.Channel)
            .ExecuteDeleteAsync(cancellationToken);

        if (authIdentifier.Channel == AuthIdentifierChannel.Phone)
        {
            await dbContext.OtpSmsOutboxMessages
                .Where(message => message.Phone == destination
                    && message.Status == OtpSmsOutboxStatus.Pending)
                .ExecuteUpdateAsync(
                    setters => setters
                        .SetProperty(message => message.Status, OtpSmsOutboxStatus.Failed)
                        .SetProperty(message => message.ProcessedAt, now),
                    cancellationToken);

            var outboxMessage = new OtpSmsOutboxMessage
            {
                OtpCode = otpCode,
                Phone = destination,
                ProtectedPayload = OtpSmsOutboxPayload.Protect(dataProtectionProvider, code),
                Status = OtpSmsOutboxStatus.Pending,
                CreatedAt = now,
            };

            dbContext.OtpCodes.Add(otpCode);
            dbContext.OtpSmsOutboxMessages.Add(outboxMessage);
        }
        else
        {
            await dbContext.OtpEmailOutboxMessages
                .Where(message => message.Email == destination
                    && message.Status == OtpSmsOutboxStatus.Pending)
                .ExecuteUpdateAsync(
                    setters => setters
                        .SetProperty(message => message.Status, OtpSmsOutboxStatus.Failed)
                        .SetProperty(message => message.ProcessedAt, now),
                    cancellationToken);

            var outboxMessage = new OtpEmailOutboxMessage
            {
                OtpCode = otpCode,
                Email = destination,
                ProtectedPayload = OtpEmailOutboxPayload.Protect(dataProtectionProvider, code),
                Status = OtpSmsOutboxStatus.Pending,
                CreatedAt = now,
            };

            dbContext.OtpCodes.Add(otpCode);
            dbContext.OtpEmailOutboxMessages.Add(outboxMessage);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Result.Ok();
    }

    private async Task<Result> EnforceSendLimitsAsync(
        AuthIdentifier authIdentifier,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var sentTimes = authIdentifier.Channel == AuthIdentifierChannel.Phone
            ? await dbContext.OtpSmsOutboxMessages
                .AsNoTracking()
                .Where(message => message.Phone == authIdentifier.NormalizedValue
                    && message.Status == OtpSmsOutboxStatus.Sent
                    && message.ProcessedAt != null)
                .OrderByDescending(message => message.ProcessedAt)
                .Select(message => message.ProcessedAt!.Value)
                .ToListAsync(cancellationToken)
            : await dbContext.OtpEmailOutboxMessages
                .AsNoTracking()
                .Where(message => message.Email == authIdentifier.NormalizedValue
                    && message.Status == OtpSmsOutboxStatus.Sent
                    && message.ProcessedAt != null)
                .OrderByDescending(message => message.ProcessedAt)
                .Select(message => message.ProcessedAt!.Value)
                .ToListAsync(cancellationToken);

        if (sentTimes.Count == 0)
        {
            return Result.Ok();
        }

        var otpOptions = options.Value;
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

        var hourlyWindowStart = now.AddHours(-1);
        var hourlySends = sentTimes.Count(sentAt => sentAt >= hourlyWindowStart);

        if (hourlySends >= otpOptions.HourlySendLimit)
        {
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

    private static string AccountExistsMessage(AuthIdentifier identifier) =>
        identifier.Channel == AuthIdentifierChannel.Email
            ? "An account already exists for this email address. Sign in instead."
            : "An account already exists for this phone number. Sign in instead.";

    private static string IdentifierTakenMessage(AuthIdentifier identifier) =>
        identifier.Channel == AuthIdentifierChannel.Email
            ? "This email address is already linked to another account."
            : "This phone number is already linked to another account.";

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
