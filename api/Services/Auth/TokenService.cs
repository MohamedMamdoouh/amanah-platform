using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Amanah.Api.Auth;
using Amanah.Api.Data;
using Amanah.Api.Data.Entities;
using Amanah.Api.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Amanah.Api.Services.Auth;

public sealed class TokenService(
    AppDbContext dbContext,
    IOptions<JwtOptions> options,
    TimeProvider timeProvider)
{
    private readonly JwtOptions _options = options.Value;

    public string IssueAccessToken(User user)
    {
        var signingKey = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(_options.AccessTokenSigningKey));
        var credentials = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);
        var now = timeProvider.GetUtcNow();

        var token = new JwtSecurityToken(
            claims: [
                new Claim(AuthClaimTypes.Sub, user.Id.ToString()),
                new Claim(AuthClaimTypes.Role, user.Role.ToString()),
                new Claim(AuthClaimTypes.Jti, Guid.NewGuid().ToString()),
            ],
            notBefore: now.UtcDateTime,
            expires: now.AddMinutes(_options.AccessTokenLifetimeMinutes).UtcDateTime,
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public async Task<(string RawToken, RefreshToken Entity)> IssueRefreshTokenAsync(
        User user,
        CancellationToken cancellationToken = default)
    {
        var rawToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        var now = timeProvider.GetUtcNow();

        var refreshToken = new RefreshToken
        {
            UserId = user.Id,
            TokenHash = RefreshTokenHasher.Hash(rawToken),
            ExpiresAt = now.AddDays(_options.RefreshTokenLifetimeDays),
            IsRevoked = false,
            CreatedAt = now,
        };

        dbContext.RefreshTokens.Add(refreshToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        return (rawToken, refreshToken);
    }

    public async Task<RefreshToken?> FindActiveRefreshTokenAsync(
        string rawToken,
        CancellationToken cancellationToken = default)
    {
        var hash = RefreshTokenHasher.Hash(rawToken);
        var now = timeProvider.GetUtcNow();

        return await dbContext.RefreshTokens
            .Include(token => token.User)
            .FirstOrDefaultAsync(
                token => token.TokenHash == hash
                    && !token.IsRevoked
                    && token.ExpiresAt > now,
                cancellationToken);
    }

    public async Task RevokeRefreshTokenAsync(
        RefreshToken refreshToken,
        CancellationToken cancellationToken = default)
    {
        refreshToken.IsRevoked = true;
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task RevokeAllRefreshTokensAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        await dbContext.RefreshTokens
            .Where(token => token.UserId == userId && !token.IsRevoked)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(token => token.IsRevoked, true),
                cancellationToken);
    }
}
