using Amanah.Api.Data.Entities;
using Amanah.Api.Models.Errors;
using Amanah.Contracts.Errors;

namespace Amanah.Api.Auth;

public static class AdminParticipation
{
    public static ResultError? ForbidIfAdmin(UserRole role)
    {
        if (role != UserRole.Admin)
        {
            return null;
        }

        return ResultError.Forbidden(
            "Administrators cannot participate as users.",
            ErrorCodes.AdminParticipationForbidden);
    }
}
