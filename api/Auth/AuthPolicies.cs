using Amanah.Api.Data.Entities;

namespace Amanah.Api.Auth;

public static class AuthPolicies
{
    public const string Admin = nameof(UserRole.Admin);

    public const string ActiveAccount = nameof(ActiveAccount);
}
