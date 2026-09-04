using System.Security.Claims;
using Amanah.Api.Data.Entities;

namespace Amanah.Api.Auth;

public static class ClaimsPrincipalExtensions
{
    public static bool TryGetUserId(this ClaimsPrincipal principal, out Guid userId)
    {
        var sub = principal.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? principal.FindFirstValue(AuthClaimTypes.Sub);

        if (sub is not null && Guid.TryParse(sub, out userId))
        {
            return true;
        }

        userId = default;
        return false;
    }

    public static UserRole GetUserRole(this ClaimsPrincipal principal)
    {
        var role = principal.FindFirstValue(AuthClaimTypes.Role);
        return Enum.TryParse<UserRole>(role, out var parsedRole)
            ? parsedRole
            : UserRole.User;
    }
}
