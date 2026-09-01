using Amanah.Api.Data;
using Amanah.Api.Data.Entities;
using Amanah.Contracts.Requests.Auth;
using Amanah.Contracts.Responses.Auth;
using Amanah.Contracts.Errors;
using Amanah.Api.Models.Errors;
using Amanah.Api.Utilities;
using Microsoft.EntityFrameworkCore;

namespace Amanah.Api.Services.Auth;

public sealed class AuthService(
    AppDbContext dbContext,
    HandoffTokenService handoffTokenService,
    TokenService tokenService,
    UserPasswordHasher passwordHasher,
    TimeProvider timeProvider)
{
    public async Task<Result<(AuthSessionResponse Session, string RawRefreshToken)>> RegisterAsync(
        RegisterRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!handoffTokenService.TryValidate(
                request.SignupToken,
                AuthTokenPurposes.Signup,
                out var normalizedPhone))
        {
            return ResultError.BadRequest(
                "The signup token is invalid or has expired.",
                ErrorCodes.HandoffTokenInvalid);
        }

        if (await dbContext.Users.AnyAsync(
                user => user.NormalizedPhone == normalizedPhone,
                cancellationToken))
        {
            return ResultError.Conflict(
                "An account already exists for this phone number.",
                ErrorCodes.Conflict);
        }

        var now = timeProvider.GetUtcNow();
        var displayName = DisplayNameValidator.Normalize(request.DisplayName);

        var user = new User
        {
            NormalizedPhone = normalizedPhone,
            DisplayName = displayName,
            Role = UserRole.User,
            CreatedAt = now,
            PasswordHash = string.Empty,
        };
        user.PasswordHash = passwordHasher.HashPassword(user, request.Password);

        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync(cancellationToken);

        return await IssueSessionAsync(user, cancellationToken);
    }

    public async Task<Result<(AuthSessionResponse Session, string RawRefreshToken)>> LoginAsync(
        LoginRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!PhoneNormalizer.TryNormalize(request.Phone, out var normalizedPhone))
        {
            return ResultError.BadRequest(
                "The phone number format is not accepted.",
                ErrorCodes.InvalidPhone);
        }

        var user = await dbContext.Users
            .FirstOrDefaultAsync(u => u.NormalizedPhone == normalizedPhone, cancellationToken);

        if (user is null
            || !passwordHasher.VerifyPassword(user, request.Password, user.PasswordHash))
        {
            return ResultError.BadRequest(
                "Phone number or password is incorrect.",
                ErrorCodes.InvalidCredentials);
        }

        var banResult = CheckBan(user);
        if (banResult is not null)
        {
            return banResult;
        }

        return await IssueSessionAsync(user, cancellationToken);
    }

    public async Task<Result<(AuthSessionResponse Session, string RawRefreshToken)>> ResetPasswordAsync(
        ResetPasswordRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!handoffTokenService.TryValidate(
                request.ResetToken,
                AuthTokenPurposes.Reset,
                out var normalizedPhone))
        {
            return ResultError.BadRequest(
                "The reset token is invalid or has expired.",
                ErrorCodes.HandoffTokenInvalid);
        }

        var user = await dbContext.Users
            .FirstOrDefaultAsync(u => u.NormalizedPhone == normalizedPhone, cancellationToken);

        if (user is null)
        {
            return ResultError.BadRequest(
                "The reset token is invalid or has expired.",
                ErrorCodes.HandoffTokenInvalid);
        }

        var banResult = CheckBan(user);
        if (banResult is not null)
        {
            return banResult;
        }

        user.PasswordHash = passwordHasher.HashPassword(user, request.Password);
        await tokenService.RevokeAllRefreshTokensAsync(user.Id, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        return await IssueSessionAsync(user, cancellationToken);
    }

    public async Task<Result<(AuthSessionResponse Session, string RawRefreshToken)>> RefreshAsync(
        string rawRefreshToken,
        CancellationToken cancellationToken = default)
    {
        var refreshToken = await tokenService.FindActiveRefreshTokenAsync(
            rawRefreshToken,
            cancellationToken);

        if (refreshToken is null)
        {
            return ResultError.Unauthorized(
                "The refresh token is invalid or has been revoked.",
                ErrorCodes.RefreshInvalid);
        }

        var banResult = CheckBan(refreshToken.User);
        if (banResult is not null)
        {
            return banResult;
        }

        await tokenService.RevokeRefreshTokenAsync(refreshToken, cancellationToken);

        return await IssueSessionAsync(refreshToken.User, cancellationToken);
    }

    public async Task<Result> LogoutAsync(
        Guid userId,
        string rawRefreshToken,
        CancellationToken cancellationToken = default)
    {
        var refreshToken = await tokenService.FindActiveRefreshTokenAsync(
            rawRefreshToken,
            cancellationToken);

        if (refreshToken is null || refreshToken.UserId != userId)
        {
            return ResultError.Unauthorized(
                "The refresh token is invalid or has been revoked.",
                ErrorCodes.RefreshInvalid);
        }

        await tokenService.RevokeRefreshTokenAsync(refreshToken, cancellationToken);
        return Result.Ok();
    }

    public async Task<Result> LogoutEverywhereAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        await tokenService.RevokeAllRefreshTokensAsync(userId, cancellationToken);
        return Result.Ok();
    }

    public async Task<Result<UserProfileResponse>> GetMeAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var user = await dbContext.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);

        if (user is null)
        {
            return ResultError.Unauthorized(
                "Authentication required.",
                ErrorCodes.Unauthorized);
        }

        return MapProfile(user);
    }

    private async Task<(AuthSessionResponse Session, string RawRefreshToken)> IssueSessionAsync(
        User user,
        CancellationToken cancellationToken)
    {
        var accessToken = tokenService.IssueAccessToken(user);
        var (rawRefreshToken, _) = await tokenService.IssueRefreshTokenAsync(user, cancellationToken);

        return (
            new AuthSessionResponse
            {
                AccessToken = accessToken,
                User = MapProfile(user),
            },
            rawRefreshToken);
    }

    private static UserProfileResponse MapProfile(User user) =>
        new()
        {
            Id = user.Id,
            DisplayName = user.DisplayName ?? string.Empty,
            Role = user.Role.ToString(),
            Phone = user.NormalizedPhone,
        };

    private static ResultError? CheckBan(User user)
    {
        if (!user.IsBanned)
        {
            return null;
        }

        var reason = string.IsNullOrWhiteSpace(user.BanReason)
            ? "Your account has been banned."
            : $"Your account has been banned: {user.BanReason}";

        return ResultError.Forbidden(reason, ErrorCodes.Banned);
    }
}
