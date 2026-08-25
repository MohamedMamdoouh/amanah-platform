using System.IdentityModel.Tokens.Jwt;

namespace Amanah.Api.Auth;

public static class AuthClaimTypes
{
    public const string Sub = JwtRegisteredClaimNames.Sub;

    public const string Jti = JwtRegisteredClaimNames.Jti;

    public const string Role = "role";

    public const string Purpose = "purpose";

    public const string Phone = "phone";
}
