using Amanah.Api.Data;
using Amanah.Api.Models.Errors;
using Amanah.Api.Services.Auth;
using Amanah.Contracts.Responses.Account;
using Microsoft.EntityFrameworkCore;

namespace Amanah.Api.Services.Account;

public sealed class AccountIdentifierService(
    AppDbContext dbContext,
    OtpService otpService)
{
    public async Task<Result<AccountIdentifiersResponse>> GetIdentifiersAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var user = await dbContext.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(existing => existing.Id == userId, cancellationToken);

        if (user is null)
        {
            return ResultError.NotFound("User not found.");
        }

        return new AccountIdentifiersResponse
        {
            Phone = user.NormalizedPhone,
            Email = user.NormalizedEmail,
        };
    }

    public async Task<Result> SendLinkOtpAsync(
        Guid userId,
        string channel,
        string identifier,
        string captchaToken,
        CancellationToken cancellationToken = default)
    {
        var user = await dbContext.Users
            .FirstOrDefaultAsync(existing => existing.Id == userId, cancellationToken);

        if (user is null)
        {
            return ResultError.NotFound("User not found.");
        }

        return await otpService.SendForLinkAsync(user, channel, identifier, captchaToken, cancellationToken);
    }

    public async Task<Result<AccountIdentifiersResponse>> VerifyLinkOtpAsync(
        Guid userId,
        string channel,
        string identifier,
        string code,
        CancellationToken cancellationToken = default)
    {
        var user = await dbContext.Users
            .FirstOrDefaultAsync(existing => existing.Id == userId, cancellationToken);

        if (user is null)
        {
            return ResultError.NotFound("User not found.");
        }

        var verifyResult = await otpService.VerifyForLinkAsync(
            user,
            channel,
            identifier,
            code,
            cancellationToken);
        if (!verifyResult.IsSuccess)
        {
            return verifyResult.Error!;
        }

        return new AccountIdentifiersResponse
        {
            Phone = user.NormalizedPhone,
            Email = user.NormalizedEmail,
        };
    }
}
