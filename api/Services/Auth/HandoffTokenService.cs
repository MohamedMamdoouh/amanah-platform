using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Amanah.Api.Auth;
using Amanah.Api.Options;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Amanah.Api.Services.Auth;

public sealed class HandoffTokenService(
    IOptions<JwtOptions> options,
    TimeProvider timeProvider)
{
    private readonly JwtOptions _options = options.Value;

    public string Issue(string normalizedPhone, string purpose)
    {
        var signingKey = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(_options.HandoffTokenSigningKey));
        var credentials = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);
        var now = timeProvider.GetUtcNow();

        var token = new JwtSecurityToken(
            claims: [
                new Claim(AuthClaimTypes.Phone, normalizedPhone),
                new Claim(AuthClaimTypes.Purpose, purpose),
            ],
            notBefore: now.UtcDateTime,
            expires: now.AddMinutes(_options.HandoffTokenLifetimeMinutes).UtcDateTime,
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public bool TryValidate(string token, string expectedPurpose, out string normalizedPhone)
    {
        normalizedPhone = string.Empty;

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
            var phone = principal.FindFirstValue(AuthClaimTypes.Phone);

            if (purpose != expectedPurpose || string.IsNullOrEmpty(phone))
            {
                return false;
            }

            normalizedPhone = phone;
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }
}
