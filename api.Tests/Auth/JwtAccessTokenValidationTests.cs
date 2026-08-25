using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Amanah.Api.Auth;
using Microsoft.IdentityModel.Tokens;

namespace Amanah.Api.Tests.Auth;

public class JwtAccessTokenValidationTests
{
    private const string SigningKey = "test-signing-key-at-least-32-characters-long!";

    [Fact]
    public void Default_inbound_mapping_hides_sub_claim_from_short_name_lookup()
    {
        var token = IssueAccessToken(Guid.NewGuid());
        var principal = Validate(token, mapInboundClaims: true);

        Assert.Null(principal.FindFirst(AuthClaimTypes.Sub));
        Assert.NotNull(principal.FindFirst(ClaimTypes.NameIdentifier));
    }

    [Fact]
    public void Access_token_is_accepted_whether_or_not_inbound_claims_are_mapped()
    {
        var mapped = Validate(IssueAccessToken(Guid.NewGuid()), mapInboundClaims: true);
        var unmapped = Validate(IssueAccessToken(Guid.NewGuid()), mapInboundClaims: false);

        Assert.False(WouldRejectAccessToken(mapped));
        Assert.False(WouldRejectAccessToken(unmapped));
    }

    [Fact]
    public void Handoff_token_is_rejected_as_an_access_token()
    {
        var token = IssueHandoffToken();
        var principal = Validate(token, mapInboundClaims: false);

        Assert.True(WouldRejectAccessToken(principal));
    }

    private static string IssueAccessToken(Guid userId)
    {
        var now = DateTime.UtcNow;
        var jwt = new JwtSecurityToken(
            claims: [
                new Claim(AuthClaimTypes.Sub, userId.ToString()),
                new Claim(AuthClaimTypes.Role, "User"),
                new Claim(AuthClaimTypes.Jti, Guid.NewGuid().ToString()),
            ],
            notBefore: now,
            expires: now.AddMinutes(15),
            signingCredentials: Credentials());

        return new JwtSecurityTokenHandler().WriteToken(jwt);
    }

    private static string IssueHandoffToken()
    {
        var now = DateTime.UtcNow;
        var jwt = new JwtSecurityToken(
            claims: [
                new Claim(AuthClaimTypes.Phone, "+201012345678"),
                new Claim(AuthClaimTypes.Purpose, "signup"),
            ],
            notBefore: now,
            expires: now.AddMinutes(15),
            signingCredentials: Credentials());

        return new JwtSecurityTokenHandler().WriteToken(jwt);
    }

    private static ClaimsPrincipal Validate(string token, bool mapInboundClaims)
    {
        var handler = new JwtSecurityTokenHandler { MapInboundClaims = mapInboundClaims };
        return handler.ValidateToken(token, new TokenValidationParameters
        {
            ValidateIssuer = false,
            ValidateAudience = false,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(SigningKey)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero,
            NameClaimType = AuthClaimTypes.Sub,
            RoleClaimType = AuthClaimTypes.Role,
        }, out _);
    }

    private static bool WouldRejectAccessToken(ClaimsPrincipal principal)
    {
        var sub = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? principal.FindFirst(AuthClaimTypes.Sub)?.Value;
        var purpose = principal.FindFirst(AuthClaimTypes.Purpose)?.Value;
        return string.IsNullOrEmpty(sub) || purpose is not null;
    }

    private static SigningCredentials Credentials() =>
        new(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(SigningKey)), SecurityAlgorithms.HmacSha256);
}
