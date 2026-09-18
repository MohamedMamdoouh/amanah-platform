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

        var isActive = await dbContext.Users
            .AsNoTracking()
            .Where(user => user.Id == userId)
            .Select(user => user.DeactivatedAt == null)
            .SingleOrDefaultAsync();

        if (isActive)
        {
            context.Succeed(requirement);
        }
    }
}
