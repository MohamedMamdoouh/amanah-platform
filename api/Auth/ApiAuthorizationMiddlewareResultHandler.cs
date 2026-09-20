using Amanah.Api.Data;
using Amanah.Api.Models.Errors;
using Amanah.Contracts.Errors;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace Amanah.Api.Auth;

public sealed class ApiAuthorizationMiddlewareResultHandler : IAuthorizationMiddlewareResultHandler
{
    private readonly AuthorizationMiddlewareResultHandler _defaultHandler = new();

    public async Task HandleAsync(
        RequestDelegate next,
        HttpContext context,
        AuthorizationPolicy policy,
        PolicyAuthorizationResult authorizeResult)
    {
        if (authorizeResult.Forbidden
            && authorizeResult.AuthorizationFailure?.FailedRequirements
                .Any(requirement => requirement is ActiveAccountRequirement) == true)
        {
            var actionContext = new ActionContext(
                context,
                context.GetRouteData(),
                new ActionDescriptor());

            var forbidden = await ResolveActiveAccountForbiddenAsync(context);
            await forbidden.ToActionResult().ExecuteResultAsync(actionContext);
            return;
        }

        await _defaultHandler.HandleAsync(next, context, policy, authorizeResult);
    }

    private static async Task<ResultError> ResolveActiveAccountForbiddenAsync(HttpContext context)
    {
        if (context.User.TryGetUserId(out var userId))
        {
            var dbContext = context.RequestServices.GetRequiredService<AppDbContext>();
            var account = await dbContext.Users
                .AsNoTracking()
                .Where(user => user.Id == userId)
                .Select(user => new { user.IsBanned, user.BanReason })
                .SingleOrDefaultAsync();

            if (account?.IsBanned == true)
            {
                var reason = string.IsNullOrWhiteSpace(account.BanReason)
                    ? "Your account has been banned."
                    : $"Your account has been banned: {account.BanReason}";
                return ResultError.Forbidden(reason, ErrorCodes.Banned);
            }
        }

        return ResultError.Forbidden(
            "This account is deactivated. Reactivate to continue.",
            ErrorCodes.AccountReactivationRequired);
    }
}
