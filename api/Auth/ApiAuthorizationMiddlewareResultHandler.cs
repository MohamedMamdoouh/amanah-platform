using Amanah.Api.Models.Errors;
using Amanah.Contracts.Errors;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;

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

            await ResultError.Forbidden(
                "This account is deactivated. Reactivate to continue.",
                ErrorCodes.AccountReactivationRequired).ToActionResult()
                .ExecuteResultAsync(actionContext);
            return;
        }

        await _defaultHandler.HandleAsync(next, context, policy, authorizeResult);
    }
}
