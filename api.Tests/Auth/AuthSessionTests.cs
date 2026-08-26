using Amanah.Api.Data;
using Amanah.Api.Data.Entities;
using Amanah.Contracts.Errors;
using Amanah.Api.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Amanah.Api.Tests.Auth;

public class AuthSessionTests(ApiWebApplicationFactory factory) : IClassFixture<ApiWebApplicationFactory>
{
    [Fact]
    public async Task Register_with_valid_signup_token_creates_user_and_returns_tokens()
    {
        await using var context = await CreateContextAsync();

        var code = await context.SendOtpAndGetCodeAsync("01012345678");
        var (_, verifyBody) = await context.VerifyOtpAsync("01012345678", code);
        var (response, session) = await context.RegisterAsync(verifyBody!.SignupToken!, "Ahmed");

        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(session?.AccessToken);
        Assert.NotNull(session.RefreshToken);
        Assert.Equal("Ahmed", session.User.DisplayName);
        Assert.Equal("User", session.User.Role);
        Assert.Equal(1, await context.DbContext.Users.CountAsync());
    }

    [Fact]
    public async Task Register_without_signup_token_returns_handoff_token_invalid()
    {
        await using var context = await CreateContextAsync();

        var (response, error) = await RegisterWithErrorAsync(context, "not-a-valid-jwt", "Ahmed");

        Assert.Equal(System.Net.HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(ErrorCodes.HandoffTokenInvalid, error?.Code);
        Assert.Equal(0, await context.DbContext.Users.CountAsync());
    }

    [Fact]
    public async Task Register_with_accept_terms_false_returns_validation_failed()
    {
        await using var context = await CreateContextAsync();

        var code = await context.SendOtpAndGetCodeAsync("01012345678");
        var (_, verifyBody) = await context.VerifyOtpAsync("01012345678", code);
        var (response, error) = await RegisterWithErrorAsync(
            context,
            verifyBody!.SignupToken!,
            "Ahmed",
            acceptTerms: false);

        Assert.Equal(System.Net.HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(ErrorCodes.ValidationFailed, error?.Code);
        Assert.Contains(
            "You must accept the terms and conditions and privacy policy.",
            error?.Errors?["acceptTerms"] ?? []);
    }

    [Fact]
    public async Task Abandoned_signup_leaves_no_user_and_allows_verify_again()
    {
        await using var context = await CreateContextAsync();

        var code = await context.SendOtpAndGetCodeAsync("01012345678");
        await context.VerifyOtpAsync("01012345678", code);

        Assert.Equal(0, await context.DbContext.Users.CountAsync());

        var code2 = await context.SendOtpAndGetCodeAsync("01012345678");
        var (verifyResponse, verifyBody) = await context.VerifyOtpAsync("01012345678", code2);

        Assert.Equal(System.Net.HttpStatusCode.OK, verifyResponse.StatusCode);
        Assert.Equal("new_user", verifyBody?.Status);
    }

    [Fact]
    public async Task Login_existing_user_returns_tokens()
    {
        await using var context = await CreateContextAsync();

        var now = DateTimeOffset.UtcNow;
        context.DbContext.Users.Add(new User
        {
            Id = Guid.NewGuid(),
            NormalizedPhone = "+201012345678",
            DisplayName = "Ahmed",
            Role = UserRole.User,
            CreatedAt = now,
        });
        await context.DbContext.SaveChangesAsync();
        context.DbContext.ChangeTracker.Clear();

        var code = await context.SendOtpAndGetCodeAsync("01012345678");
        var (_, verifyBody) = await context.VerifyOtpAsync("01012345678", code);
        var (response, session) = await context.LoginAsync("01012345678", verifyBody!.LoginToken!);

        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(session?.AccessToken);
        Assert.NotNull(session.RefreshToken);
    }

    [Fact]
    public async Task Login_banned_user_returns_banned_with_reason()
    {
        await using var context = await CreateContextAsync();

        var now = DateTimeOffset.UtcNow;
        context.DbContext.Users.Add(new User
        {
            Id = Guid.NewGuid(),
            NormalizedPhone = "+201012345678",
            DisplayName = "Ahmed",
            Role = UserRole.User,
            IsBanned = true,
            BanReason = "policy violations",
            CreatedAt = now,
        });
        await context.DbContext.SaveChangesAsync();
        context.DbContext.ChangeTracker.Clear();

        var code = await context.SendOtpAndGetCodeAsync("01012345678");
        var (_, verifyBody) = await context.VerifyOtpAsync("01012345678", code);
        var (response, error) = await LoginWithErrorAsync(
            context,
            "01012345678",
            verifyBody!.LoginToken!);

        Assert.Equal(System.Net.HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(ErrorCodes.Banned, error?.Code);
        Assert.Contains("policy violations", error?.Message);
    }

    [Fact]
    public async Task Refresh_with_valid_token_rotates_and_revokes_old_token()
    {
        await using var context = await CreateContextAsync();

        var (session, _) = await context.RegisterNewUserAsync();
        var oldRefresh = session.RefreshToken;

        var (refreshResponse, newSession) = await context.RefreshAsync(oldRefresh);

        Assert.Equal(System.Net.HttpStatusCode.OK, refreshResponse.StatusCode);
        Assert.NotNull(newSession?.AccessToken);
        Assert.NotEqual(oldRefresh, newSession.RefreshToken);

        var (retryResponse, retryError) = await RefreshWithErrorAsync(context, oldRefresh);
        Assert.Equal(System.Net.HttpStatusCode.Unauthorized, retryResponse.StatusCode);
        Assert.Equal(ErrorCodes.RefreshInvalid, retryError?.Code);
    }

    [Fact]
    public async Task Refresh_banned_user_returns_banned()
    {
        await using var context = await CreateContextAsync();

        var (session, _) = await context.RegisterNewUserAsync();
        var user = await context.DbContext.Users.SingleAsync();
        user.IsBanned = true;
        user.BanReason = "abuse";
        await context.DbContext.SaveChangesAsync();
        context.DbContext.ChangeTracker.Clear();

        var (response, error) = await RefreshWithErrorAsync(context, session.RefreshToken);

        Assert.Equal(System.Net.HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(ErrorCodes.Banned, error?.Code);
    }

    [Fact]
    public async Task Logout_revokes_refresh_token()
    {
        await using var context = await CreateContextAsync();

        var (session, _) = await context.RegisterNewUserAsync();
        var logoutResponse = await context.LogoutAsync(session.AccessToken, session.RefreshToken);

        Assert.Equal(System.Net.HttpStatusCode.NoContent, logoutResponse.StatusCode);

        var (refreshResponse, error) = await RefreshWithErrorAsync(context, session.RefreshToken);
        Assert.Equal(System.Net.HttpStatusCode.Unauthorized, refreshResponse.StatusCode);
        Assert.Equal(ErrorCodes.RefreshInvalid, error?.Code);
    }

    [Fact]
    public async Task Logout_everywhere_revokes_all_refresh_tokens()
    {
        await using var context = await CreateContextAsync();

        var (session, _) = await context.RegisterNewUserAsync();
        var (_, session2) = await context.RefreshAsync(session.RefreshToken);

        var logoutResponse = await context.LogoutEverywhereAsync(session2!.AccessToken);
        Assert.Equal(System.Net.HttpStatusCode.NoContent, logoutResponse.StatusCode);

        var (refresh1, _) = await RefreshWithErrorAsync(context, session.RefreshToken);
        var (refresh2, _) = await RefreshWithErrorAsync(context, session2.RefreshToken);

        Assert.Equal(System.Net.HttpStatusCode.Unauthorized, refresh1.StatusCode);
        Assert.Equal(System.Net.HttpStatusCode.Unauthorized, refresh2.StatusCode);
    }

    [Fact]
    public async Task Me_returns_profile_for_authenticated_user()
    {
        await using var context = await CreateContextAsync();

        var (session, _) = await context.RegisterNewUserAsync();
        var (response, profile) = await context.GetMeAsync(session.AccessToken);

        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(session.User.Id, profile?.Id);
        Assert.Equal("Ahmed", profile?.DisplayName);
        Assert.Equal("User", profile?.Role);
        Assert.Equal("+201012345678", profile?.Phone);
    }

    [Fact]
    public async Task Me_without_token_returns_unauthorized()
    {
        await using var context = await CreateContextAsync();

        var (response, error) = await GetMeWithErrorAsync(context);

        Assert.Equal(System.Net.HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(ErrorCodes.Unauthorized, error?.Code);
    }

    [Fact]
    public async Task Me_with_signup_handoff_token_as_bearer_returns_unauthorized()
    {
        await using var context = await CreateContextAsync();

        var code = await context.SendOtpAndGetCodeAsync("01012345678");
        var (_, verifyBody) = await context.VerifyOtpAsync("01012345678", code);
        var (response, _) = await context.GetMeAsync(verifyBody!.SignupToken);

        Assert.Equal(System.Net.HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private static async Task<(HttpResponseMessage, ApiError?)> RegisterWithErrorAsync(
        OtpSendTestContext context,
        string signupToken,
        string displayName,
        bool acceptTerms = true)
    {
        var (response, _) = await context.RegisterAsync(signupToken, displayName, acceptTerms);
        var error = await context.ReadErrorAsync(response);
        return (response, error);
    }

    private static async Task<(HttpResponseMessage, ApiError?)> LoginWithErrorAsync(
        OtpSendTestContext context,
        string phone,
        string loginToken)
    {
        var (response, _) = await context.LoginAsync(phone, loginToken);
        var error = await context.ReadErrorAsync(response);
        return (response, error);
    }

    private static async Task<(HttpResponseMessage, ApiError?)> RefreshWithErrorAsync(
        OtpSendTestContext context,
        string refreshToken)
    {
        var (response, _) = await context.RefreshAsync(refreshToken);
        var error = await context.ReadErrorAsync(response);
        return (response, error);
    }

    private static async Task<(HttpResponseMessage, ApiError?)> GetMeWithErrorAsync(
        OtpSendTestContext context)
    {
        var (response, _) = await context.GetMeAsync();
        var error = await context.ReadErrorAsync(response);
        return (response, error);
    }

    private async Task<OtpSendTestContext> CreateContextAsync()
    {
        factory.CaptchaVerifier.ShouldSucceed = true;
        factory.SmsSender.ShouldThrow = false;
        factory.SmsSender.ShouldTimeout = false;
        factory.SmsSender.SentMessages.Clear();

        await using var setupScope = factory.Services.CreateAsyncScope();
        var setupContext = setupScope.ServiceProvider.GetRequiredService<AppDbContext>();
        await setupContext.Database.MigrateAsync();
        await setupContext.OtpCodes.ExecuteDeleteAsync();
        await setupContext.OtpSmsOutboxMessages.ExecuteDeleteAsync();
        await setupContext.RefreshTokens.ExecuteDeleteAsync();
        await setupContext.Users.ExecuteDeleteAsync();

        var scope = factory.Services.CreateAsyncScope();
        return new OtpSendTestContext(
            factory.CreateClient(),
            factory.SmsSender,
            factory.CaptchaVerifier,
            scope);
    }
}
