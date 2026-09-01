using Amanah.Api.Data;
using Amanah.Api.Services.Auth;
using Amanah.Api.Tests.Infrastructure;
using Amanah.Contracts.Errors;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Amanah.Api.Tests.Auth;

public class PasswordResetTests(ApiWebApplicationFactory factory) : IClassFixture<ApiWebApplicationFactory>
{
    [Fact]
    public async Task Password_reset_flow_updates_password_and_signs_in()
    {
        await using var context = await CreateContextAsync();

        await context.RegisterNewUserAsync();

        var code = await context.SendOtpAndGetCodeAsync("01012345678", OtpPurposes.PasswordReset);
        var (_, verifyBody) = await context.VerifyOtpAsync(
            "01012345678",
            code,
            OtpPurposes.PasswordReset);

        var (response, session) = await context.ResetPasswordAsync(
            verifyBody!.ResetToken!,
            "NewPass123");

        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(session?.AccessToken);
        OtpSendTestContext.AssertRefreshCookieSet(response);

        var (loginResponse, _) = await context.LoginAsync("01012345678", "NewPass123");
        Assert.Equal(System.Net.HttpStatusCode.OK, loginResponse.StatusCode);

        var (oldLoginResponse, oldLoginError) = await LoginWithErrorAsync(
            context,
            "01012345678",
            TestAuthHelpers.DefaultPassword);
        Assert.Equal(System.Net.HttpStatusCode.BadRequest, oldLoginResponse.StatusCode);
        Assert.Equal(ErrorCodes.InvalidCredentials, oldLoginError?.Code);
    }

    [Fact]
    public async Task Password_reset_revokes_existing_refresh_tokens()
    {
        await using var context = await CreateContextAsync();

        var code = await context.SendOtpAndGetCodeAsync("01012345678");
        var (_, signupVerify) = await context.VerifyOtpAsync("01012345678", code);
        var (registerResponse, _) = await context.RegisterAsync(signupVerify!.SignupToken!);
        var oldRefresh = OtpSendTestContext.ExtractRefreshToken(registerResponse);
        Assert.NotNull(oldRefresh);

        var resetCode = await context.SendOtpAndGetCodeAsync("01012345678", OtpPurposes.PasswordReset);
        var (_, verifyBody) = await context.VerifyOtpAsync(
            "01012345678",
            resetCode,
            OtpPurposes.PasswordReset);

        await context.ResetPasswordAsync(verifyBody!.ResetToken!, "NewPass123");

        var (refreshResponse, refreshError) = await RefreshWithErrorAsync(context, oldRefresh);
        Assert.Equal(System.Net.HttpStatusCode.Unauthorized, refreshResponse.StatusCode);
        Assert.Equal(ErrorCodes.RefreshInvalid, refreshError?.Code);
    }

    [Fact]
    public async Task Password_reset_send_for_unknown_phone_returns_204_without_sms()
    {
        await using var context = await CreateContextAsync();

        var response = await context.SendOtpAsync("01099998888", OtpPurposes.PasswordReset);

        Assert.Equal(System.Net.HttpStatusCode.NoContent, response.StatusCode);
        Assert.Empty(context.SmsSender.SentMessages);
        Assert.Equal(0, await context.DbContext.OtpCodes.CountAsync());
    }

    [Fact]
    public async Task Signup_otp_send_for_existing_phone_returns_account_exists()
    {
        await using var context = await CreateContextAsync();

        await context.RegisterNewUserAsync();

        var response = await context.SendOtpAsync("01012345678", OtpPurposes.Signup);
        var error = await context.ReadErrorAsync(response);

        Assert.Equal(System.Net.HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(ErrorCodes.AccountExists, error?.Code);
        Assert.Empty(context.SmsSender.SentMessages);
    }

    private static async Task<(HttpResponseMessage, ApiError?)> LoginWithErrorAsync(
        OtpSendTestContext context,
        string phone,
        string password)
    {
        var (response, _) = await context.LoginAsync(phone, password);
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
        var client = factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            HandleCookies = true,
        });

        return new OtpSendTestContext(
            client,
            factory.SmsSender,
            factory.CaptchaVerifier,
            scope);
    }
}
