using Amanah.Api.Data;
using Amanah.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Amanah.Api.Tests;

public class AuthDatabaseTests(ApiWebApplicationFactory factory) : IClassFixture<ApiWebApplicationFactory>
{
    [Fact]
    public async Task User_can_be_inserted_and_retrieved_by_normalized_phone()
    {
        await RunWithMigratedContextAsync(async context =>
        {
            const string phone = "+201012345678";
            var user = new User
            {
                NormalizedPhone = phone,
                Role = UserRole.User,
                CreatedAt = DateTimeOffset.UtcNow,
            };

            context.Users.Add(user);
            await context.SaveChangesAsync();

            Assert.NotEqual(Guid.Empty, user.Id);

            var found = await context.Users.SingleAsync(u => u.NormalizedPhone == phone);

            Assert.Equal(user.Id, found.Id);
            Assert.Equal(phone, found.NormalizedPhone);
            Assert.Equal(UserRole.User, found.Role);
            Assert.False(found.IsBanned);
        });
    }

    [Fact]
    public async Task Duplicate_normalized_phone_is_rejected()
    {
        await RunWithMigratedContextAsync(async context =>
        {
            const string phone = "+201098765432";
            context.Users.Add(new User
            {
                NormalizedPhone = phone,
                Role = UserRole.User,
                CreatedAt = DateTimeOffset.UtcNow,
            });
            await context.SaveChangesAsync();

            context.Users.Add(new User
            {
                NormalizedPhone = phone,
                Role = UserRole.User,
                CreatedAt = DateTimeOffset.UtcNow,
            });

            await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
        });
    }

    [Fact]
    public async Task OtpCode_and_RefreshToken_can_be_inserted_with_valid_user_fk()
    {
        await RunWithMigratedContextAsync(async context =>
        {
            var user = new User
            {
                NormalizedPhone = "+201055544433",
                Role = UserRole.User,
                CreatedAt = DateTimeOffset.UtcNow,
            };

            context.Users.Add(user);
            await context.SaveChangesAsync();

            var now = DateTimeOffset.UtcNow;
            context.OtpCodes.Add(new OtpCode
            {
                Phone = user.NormalizedPhone,
                CodeHash = "otp-hash",
                ExpiresAt = now.AddMinutes(10),
                CreatedAt = now,
            });

            context.RefreshTokens.Add(new RefreshToken
            {
                UserId = user.Id,
                TokenHash = "refresh-hash",
                ExpiresAt = now.AddDays(30),
                CreatedAt = now,
            });

            await context.SaveChangesAsync();

            var otpCode = await context.OtpCodes.SingleAsync(code => code.Phone == user.NormalizedPhone);
            var refreshToken = await context.RefreshTokens.SingleAsync(token => token.UserId == user.Id);

            Assert.Equal("otp-hash", otpCode.CodeHash);
            Assert.Equal("refresh-hash", refreshToken.TokenHash);
            Assert.False(refreshToken.IsRevoked);
        });
    }

    private async Task RunWithMigratedContextAsync(Func<AppDbContext, Task> test)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await context.Database.MigrateAsync();
        await test(context);
    }
}
