using Amanah.Api.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;

namespace Amanah.Api.Auth;

public sealed class ActiveAccountAuthorizationHandler(AppDbContext dbContext)
    : AuthorizationHandler<ActiveAccountRequirement>
{
    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        ActiveAccountRequirement requirement)
    {
        if (!context.User.TryGetUserId(out var userId))
        {
            return;
        }

        // Ban must block ActiveAccount (and Admin) even while a pre-ban access JWT
        // remains valid — refresh revocation alone leaves a ~AccessTokenLifetimeMinutes window.
        var account = await dbContext.Users
            .AsNoTracking()
            .Where(user => user.Id == userId)
            .Select(user => new { user.IsBanned, user.DeactivatedAt })
            .SingleOrDefaultAsync();

        if (account is not null && !account.IsBanned && account.DeactivatedAt is null)
        {
            context.Succeed(requirement);
        }
    }
}
