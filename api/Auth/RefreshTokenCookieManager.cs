using Amanah.Api.Options;
using Microsoft.Extensions.Options;

namespace Amanah.Api.Auth;

public sealed class RefreshTokenCookieManager(
    IOptions<JwtOptions> jwtOptions,
    IHostEnvironment environment)
{
    public const string CookieName = "amanah_refresh";

    private const string Path = "/api/v1/auth";

    public void Set(HttpResponse response, string rawRefreshToken)
    {
        var maxAge = TimeSpan.FromDays(jwtOptions.Value.RefreshTokenLifetimeDays);
        response.Cookies.Append(CookieName, rawRefreshToken, CreateCookieOptions(maxAge));
    }

    public string? Get(HttpRequest request) =>
        request.Cookies.TryGetValue(CookieName, out var value) ? value : null;

    public void Clear(HttpResponse response) =>
        response.Cookies.Delete(CookieName, CreateCookieOptions());

    private CookieOptions CreateCookieOptions(TimeSpan? maxAge = null) =>
        new()
        {
            HttpOnly = true,
            Secure = !environment.IsDevelopment(),
            SameSite = SameSiteMode.Lax,
            Path = Path,
            MaxAge = maxAge,
        };
}
