using Amanah.Api.Models.Errors;
using Amanah.Contracts.Errors;
using Microsoft.AspNetCore.Mvc;

namespace Amanah.Api.Auth;

public static class ControllerBaseAuthExtensions
{
    public static IActionResult? RequireUserId(this ControllerBase controller, out Guid userId)
    {
        if (controller.User.TryGetUserId(out userId))
        {
            return null;
        }

        userId = default;
        return ResultError.Unauthorized(
            "Authentication required.",
            ErrorCodes.Unauthorized).ToActionResult();
    }
}
