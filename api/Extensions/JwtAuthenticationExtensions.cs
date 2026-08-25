using Amanah.Api.Auth;
using Amanah.Api.Options;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

namespace Amanah.Api.Extensions;

public static class JwtAuthenticationExtensions
{
    public static IServiceCollection AddJwtAuthentication(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var jwtOptions = configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>()
            ?? new JwtOptions();

        var signingKey = new SymmetricSecurityKey(
            System.Text.Encoding.UTF8.GetBytes(jwtOptions.AccessTokenSigningKey));

        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = false,
                    ValidateAudience = false,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = signingKey,
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.Zero,
                    NameClaimType = AuthClaimTypes.Sub,
                    RoleClaimType = AuthClaimTypes.Role,
                };
            });

        services.AddAuthorizationBuilder()
            .AddPolicy(AuthPolicies.Admin, policy =>
                policy.RequireRole(AuthPolicies.Admin));

        return services;
    }
}
