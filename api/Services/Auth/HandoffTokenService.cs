using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Amanah.Api.Auth;
using Amanah.Api.Options;
using Amanah.Contracts.Requests.Auth;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Amanah.Api.Services.Auth;

public sealed class HandoffTokenService(
    IOptions<JwtOptions> options,
    TimeProvider timeProvider)
{
    private readonly JwtOptions _options = options.Value;

    public string Issue(AuthIdentifier identity, string purpose)
    {
        var signingKey = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(_options.HandoffTokenSigningKey));
        var credentials = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);
        var now = timeProvider.GetUtcNow();

        var token = new JwtSecurityToken(
            claims: [
                new Claim(AuthClaimTypes.Channel, ChannelToClaim(identity.Channel)),
                new Claim(AuthClaimTypes.Identifier, identity.NormalizedValue),
                new Claim(AuthClaimTypes.Purpose, purpose),
            ],
            notBefore: now.UtcDateTime,
            expires: now.AddMinutes(_options.HandoffTokenLifetimeMinutes).UtcDateTime,
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public bool TryValidate(string token, string expectedPurpose, out AuthIdentifier identity)
    {
        identity = default;

        if (string.IsNullOrWhiteSpace(token))
        {
            return false;
        }

        var signingKey = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(_options.HandoffTokenSigningKey));
        var validationParameters = new TokenValidationParameters
        {
            ValidateIssuer = false,
            ValidateAudience = false,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = signingKey,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero,
        };

        try
        {
            var principal = new JwtSecurityTokenHandler().ValidateToken(
                token,
                validationParameters,
                out _);

            var purpose = principal.FindFirstValue(AuthClaimTypes.Purpose);
            if (purpose != expectedPurpose)
            {
                return false;
            }

            var channelClaim = principal.FindFirstValue(AuthClaimTypes.Channel);
            var identifier = principal.FindFirstValue(AuthClaimTypes.Identifier);

            if (string.IsNullOrEmpty(identifier)
                || !AuthIdentifierNormalizer.TryParseChannel(channelClaim, out var channel))
            {
                return false;
            }

            identity = new AuthIdentifier(channel, identifier);
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    private static string ChannelToClaim(AuthIdentifierChannel channel) =>
        channel switch
        {
            AuthIdentifierChannel.Phone => AuthIdentifierChannels.Phone,
            AuthIdentifierChannel.Email => AuthIdentifierChannels.Email,
            _ => throw new ArgumentOutOfRangeException(nameof(channel)),
        };
}
