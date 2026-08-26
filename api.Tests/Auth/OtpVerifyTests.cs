using System.IdentityModel.Tokens.Jwt;
using Amanah.Api.Auth;
using Amanah.Api.Data;
using Amanah.Api.Data.Entities;
using Amanah.Contracts.Errors;
using Amanah.Api.Services.Auth;
using Amanah.Api.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Amanah.Api.Tests.Auth;

public class OtpVerifyTests(ApiWebApplicationFactory factory) : IClassFixture<ApiWebApplicationFactory>
{
    [Fact]
    public async Task Correct_code_consumes_otp_and_returns_new_user_with_signup_token()
    {
        await using var context = await CreateContextAsync();

        var code = await context.SendOtpAndGetCodeAsync("01012345678");
        var (response, body) = await context.VerifyOtpAsync("01012345678", code);

        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("new_user", body?.Status);
        Assert.NotNull(body?.SignupToken);
        Assert.Null(body?.LoginToken);
        Assert.Equal(0, await context.DbContext.OtpCodes.CountAsync());

        using var scope = factory.Services.CreateScope();
        var tokenService = scope.ServiceProvider.GetRequiredService<HandoffTokenService>();
        Assert.True(tokenService.TryValidate(body!.SignupToken!, AuthTokenPurposes.Signup, out var phone));
        Assert.Equal("+201012345678", phone);
    }

    [Fact]
    public async Task Correct_code_for_existing_user_returns_existing_user_with_login_token()
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
        var (response, body) = await context.VerifyOtpAsync("01012345678", code);

        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("existing_user", body?.Status);
        Assert.Null(body?.SignupToken);
        Assert.NotNull(body?.LoginToken);
        Assert.Equal(0, await context.DbContext.OtpCodes.CountAsync());

        using var scope = factory.Services.CreateScope();
        var tokenService = scope.ServiceProvider.GetRequiredService<HandoffTokenService>();
        Assert.True(tokenService.TryValidate(body!.LoginToken!, AuthTokenPurposes.Login, out var phone));
        Assert.Equal("+201012345678", phone);
    }

    [Fact]
    public async Task Wrong_code_first_attempt_returns_invalid_otp_and_increments_attempt_count()
    {
        await using var context = await CreateContextAsync();

        await context.SendOtpAndGetCodeAsync("01012345678");
        var (response, error) = await VerifyWithErrorAsync(context, "01012345678", "000000");

        Assert.Equal(System.Net.HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(ErrorCodes.InvalidOtp, error?.Code);

        var otpCode = await context.DbContext.OtpCodes.SingleAsync();
        Assert.Equal(1, otpCode.AttemptCount);
    }

    [Fact]
    public async Task Wrong_code_third_attempt_voids_code_and_returns_otp_void()
    {
        await using var context = await CreateContextAsync();

        await context.SendOtpAndGetCodeAsync("01012345678");

        for (var attempt = 0; attempt < 2; attempt++)
        {
            var (response, error) = await VerifyWithErrorAsync(context, "01012345678", "000000");
            Assert.Equal(System.Net.HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Equal(ErrorCodes.InvalidOtp, error?.Code);
        }

        var (finalResponse, finalError) = await VerifyWithErrorAsync(context, "01012345678", "000000");

        Assert.Equal(System.Net.HttpStatusCode.BadRequest, finalResponse.StatusCode);
        Assert.Equal(ErrorCodes.OtpVoid, finalError?.Code);
        Assert.Equal(0, await context.DbContext.OtpCodes.CountAsync());
    }

    [Fact]
    public async Task Expired_code_returns_otp_expired_and_removes_row()
    {
        await using var context = await CreateContextAsync();

        await context.SendOtpAndGetCodeAsync("01012345678");

        var otpCode = await context.DbContext.OtpCodes.SingleAsync();
        otpCode.ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(-1);
        await context.DbContext.SaveChangesAsync();
        context.DbContext.ChangeTracker.Clear();

        var code = context.SmsSender.SentMessages[^1].Code;
        var (response, error) = await VerifyWithErrorAsync(context, "01012345678", code);

        Assert.Equal(System.Net.HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(ErrorCodes.OtpExpired, error?.Code);
        Assert.Equal(0, await context.DbContext.OtpCodes.CountAsync());
    }

    [Fact]
    public async Task Verify_after_void_without_resend_returns_otp_expired()
    {
        await using var context = await CreateContextAsync();

        await context.SendOtpAndGetCodeAsync("01012345678");

        for (var attempt = 0; attempt < 3; attempt++)
        {
            await VerifyWithErrorAsync(context, "01012345678", "000000");
        }

        var (response, error) = await VerifyWithErrorAsync(context, "01012345678", "000000");

        Assert.Equal(System.Net.HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(ErrorCodes.OtpExpired, error?.Code);
    }

    [Fact]
    public async Task Signup_token_rejected_for_login_purpose()
    {
        await using var context = await CreateContextAsync();

        var code = await context.SendOtpAndGetCodeAsync("01012345678");
        var (_, body) = await context.VerifyOtpAsync("01012345678", code);

        using var scope = factory.Services.CreateScope();
        var tokenService = scope.ServiceProvider.GetRequiredService<HandoffTokenService>();

        Assert.False(tokenService.TryValidate(body!.SignupToken!, AuthTokenPurposes.Login, out _));
    }

    [Fact]
    public async Task Signup_token_contains_signup_purpose_claim()
    {
        await using var context = await CreateContextAsync();

        var code = await context.SendOtpAndGetCodeAsync("01012345678");
        var (_, body) = await context.VerifyOtpAsync("01012345678", code);

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(body!.SignupToken);
        Assert.Equal(AuthTokenPurposes.Signup, jwt.Claims.Single(claim => claim.Type == AuthClaimTypes.Purpose).Value);
    }

    [Fact]
    public async Task Arabic_indic_otp_digits_are_accepted()
    {
        await using var context = await CreateContextAsync();

        var code = await context.SendOtpAndGetCodeAsync("01012345678");
        var arabicCode = string.Concat(code.Select(digit => (char)(digit + '٠' - '0')));
        var (response, body) = await context.VerifyOtpAsync("01012345678", arabicCode);

        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("new_user", body?.Status);
    }

    private static async Task<(HttpResponseMessage Response, ApiError? Error)> VerifyWithErrorAsync(
        OtpSendTestContext context,
        string phone,
        string code)
    {
        var (response, _) = await context.VerifyOtpAsync(phone, code);
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
        await setupContext.Users.ExecuteDeleteAsync();

        var scope = factory.Services.CreateAsyncScope();
        return new OtpSendTestContext(
            factory.CreateClient(),
            factory.SmsSender,
            factory.CaptchaVerifier,
            scope);
    }
}
