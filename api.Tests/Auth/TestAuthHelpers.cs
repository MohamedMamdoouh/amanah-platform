using Amanah.Api.Data.Entities;
using Amanah.Api.Services.Auth;

namespace Amanah.Api.Tests.Auth;

public static class TestAuthHelpers
{
    public const string DefaultPassword = "TestPass123";

    public static User CreateUser(
        UserPasswordHasher passwordHasher,
        string normalizedPhone = "+201012345678",
        string displayName = "Ahmed",
        UserRole role = UserRole.User,
        string password = DefaultPassword,
        bool isBanned = false,
        string? banReason = null)
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            NormalizedPhone = normalizedPhone,
            DisplayName = displayName,
            Role = role,
            IsBanned = isBanned,
            BanReason = banReason,
            CreatedAt = DateTimeOffset.UtcNow,
            PasswordHash = string.Empty,
        };
        user.PasswordHash = passwordHasher.HashPassword(user, password);
        return user;
    }
}
