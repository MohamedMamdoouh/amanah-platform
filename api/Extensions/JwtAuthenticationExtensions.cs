using Amanah.Api.Auth;
using Amanah.Api.Options;
using Amanah.Contracts.Chats;
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
                options.MapInboundClaims = false;

                options.Events = new JwtBearerEvents
                {
                    OnMessageReceived = context =>
                    {
                        var accessToken = context.Request.Query["access_token"];
                        var path = context.HttpContext.Request.Path;
                        if (!string.IsNullOrEmpty(accessToken)
                            && path.StartsWithSegments(ChatHubRoutes.Path))
                        {
                            context.Token = accessToken;
                        }

                        return Task.CompletedTask;
                    },
                };

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
