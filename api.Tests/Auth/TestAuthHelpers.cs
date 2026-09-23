using Amanah.Api.Data.Entities;
using Amanah.Api.Services.Auth;

namespace Amanah.Api.Tests.Auth;

public static class TestAuthHelpers
{
    public const string DefaultPassword = "TestPass123";

    public static User CreateUser(
        UserPasswordHasher passwordHasher,
        string? normalizedPhone = "+201012345678",
        string displayName = "Ahmed",
        UserRole role = UserRole.User,
        string password = DefaultPassword,
        bool isBanned = false,
        string? banReason = null,
        string? normalizedEmail = null)
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            NormalizedPhone = normalizedPhone,
            NormalizedEmail = normalizedEmail,
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

    public static User CreateEmailUser(
        UserPasswordHasher passwordHasher,
        string normalizedEmail = "user@example.com",
        string displayName = "Ahmed",
        UserRole role = UserRole.User,
        string password = DefaultPassword) =>
        CreateUser(
            passwordHasher,
            normalizedPhone: null,
            displayName: displayName,
            role: role,
            password: password,
            normalizedEmail: normalizedEmail);
}
