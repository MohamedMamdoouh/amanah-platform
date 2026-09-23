using System.Net.Http.Headers;
using System.Net.Http.Json;
using Amanah.Api.Data;
using Amanah.Api.Data.Entities;
using Amanah.Api.Services.Auth;
using Amanah.Contracts.Errors;
using Amanah.Contracts.Requests.Auth;
using Amanah.Api.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Amanah.Api.Tests.Auth;

public class EmailAuthTests(ApiWebApplicationFactory factory) : IClassFixture<ApiWebApplicationFactory>
{
    private const string TestEmail = "new.user@example.com";

    [Fact]
    public async Task Email_signup_login_and_reset_password_work_end_to_end()
    {
        await using var context = await CreateContextAsync();

        var code = await context.SendOtpAndGetEmailCodeAsync(TestEmail);
        var (_, verifyBody) = await context.VerifyOtpAsync(
            TestEmail,
            code,
            channel: AuthIdentifierChannels.Email);
        var (_, session) = await context.RegisterAsync(verifyBody!.SignupToken!, "Sara");

        Assert.Equal(TestEmail, session?.User.Email);
        Assert.Null(session?.User.Phone);

        var (loginResponse, loginSession) = await context.LoginAsync(
            TestEmail,
            channel: AuthIdentifierChannels.Email);
        Assert.Equal(System.Net.HttpStatusCode.OK, loginResponse.StatusCode);
        Assert.Equal(TestEmail, loginSession?.User.Email);

        var resetCode = await context.SendOtpAndGetEmailCodeAsync(TestEmail, OtpPurposes.PasswordReset);
        var (_, resetVerify) = await context.VerifyOtpAsync(
            TestEmail,
            resetCode,
            OtpPurposes.PasswordReset,
            AuthIdentifierChannels.Email);
        var (resetResponse, _) = await context.ResetPasswordAsync(resetVerify!.ResetToken!, "NewPass456");
        Assert.Equal(System.Net.HttpStatusCode.OK, resetResponse.StatusCode);

        var (newLoginResponse, _) = await context.LoginAsync(
            TestEmail,
            "NewPass456",
            AuthIdentifierChannels.Email);
        Assert.Equal(System.Net.HttpStatusCode.OK, newLoginResponse.StatusCode);
    }

    [Fact]
    public async Task Duplicate_email_signup_returns_account_exists()
    {
        await using var context = await CreateContextAsync();

        context.DbContext.Users.Add(TestAuthHelpers.CreateEmailUser(context.PasswordHasher, TestEmail));
        await context.DbContext.SaveChangesAsync();
        context.DbContext.ChangeTracker.Clear();

        var response = await context.SendOtpAsync(
            TestEmail,
            channel: AuthIdentifierChannels.Email);
        Assert.Equal(System.Net.HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Password_reset_for_unknown_email_returns_no_content_without_email()
    {
        await using var context = await CreateContextAsync();

        var response = await context.SendOtpAsync(
            "missing@example.com",
            OtpPurposes.PasswordReset,
            channel: AuthIdentifierChannels.Email);
        Assert.Equal(System.Net.HttpStatusCode.NoContent, response.StatusCode);
        Assert.Empty(context.EmailSender.SentMessages);
    }

    [Fact]
    public async Task Link_email_to_phone_account_after_otp_verify()
    {
        await using var context = await CreateContextAsync();
        var (session, _) = await context.RegisterNewUserAsync();

        var sendResponse = await PostAuthorizedAsync(
            context,
            session.AccessToken,
            "/api/v1/account/identifiers/otp/send",
            new { channel = AuthIdentifierChannels.Email, identifier = TestEmail, captchaToken = "valid-token" });

        Assert.Equal(System.Net.HttpStatusCode.NoContent, sendResponse.StatusCode);

        await context.WaitForEmailCountAsync(1);
        var code = context.EmailSender.SentMessages[^1].Code;

        var verifyResponse = await PostAuthorizedAsync(
            context,
            session.AccessToken,
            "/api/v1/account/identifiers/otp/verify",
            new { channel = AuthIdentifierChannels.Email, identifier = TestEmail, code });

        Assert.Equal(System.Net.HttpStatusCode.OK, verifyResponse.StatusCode);

        var (meResponse, profile) = await context.GetMeAsync(session.AccessToken);
        Assert.Equal(System.Net.HttpStatusCode.OK, meResponse.StatusCode);
        Assert.Equal(TestEmail, profile?.Email);
        Assert.NotNull(profile?.Phone);
    }

    [Fact]
    public async Task Link_identifier_already_owned_by_another_account_returns_conflict()
    {
        await using var context = await CreateContextAsync();

        context.DbContext.Users.Add(TestAuthHelpers.CreateEmailUser(context.PasswordHasher, TestEmail));
        await context.DbContext.SaveChangesAsync();
        context.DbContext.ChangeTracker.Clear();

        var (session, _) = await context.RegisterNewUserAsync();

        var sendResponse = await PostAuthorizedAsync(
            context,
            session.AccessToken,
            "/api/v1/account/identifiers/otp/send",
            new { channel = AuthIdentifierChannels.Email, identifier = TestEmail, captchaToken = "valid-token" });

        Assert.Equal(System.Net.HttpStatusCode.Conflict, sendResponse.StatusCode);
    }

    [Fact]
    public async Task User_without_identifiers_violates_database_check_constraint()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await dbContext.Database.MigrateAsync();

        dbContext.Users.Add(new User
        {
            DisplayName = "Invalid",
            Role = UserRole.User,
            CreatedAt = DateTimeOffset.UtcNow,
            PasswordHash = "hash",
        });

        await Assert.ThrowsAsync<DbUpdateException>(() => dbContext.SaveChangesAsync());
    }

    private static async Task<HttpResponseMessage> PostAuthorizedAsync(
        OtpSendTestContext context,
        string accessToken,
        string path,
        object body)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, path)
        {
            Content = JsonContent.Create(body),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return await context.Client.SendAsync(request);
    }

    private async Task<OtpSendTestContext> CreateContextAsync()
    {
        factory.CaptchaVerifier.ShouldSucceed = true;
        factory.SmsSender.SentMessages.Clear();
        factory.OtpEmailSender.SentMessages.Clear();

        await using var setupScope = factory.Services.CreateAsyncScope();
        var setupContext = setupScope.ServiceProvider.GetRequiredService<AppDbContext>();
        await setupContext.Database.MigrateAsync();
        await setupContext.OtpCodes.ExecuteDeleteAsync();
        await setupContext.OtpSmsOutboxMessages.ExecuteDeleteAsync();
        await setupContext.OtpEmailOutboxMessages.ExecuteDeleteAsync();
        await setupContext.Users.ExecuteDeleteAsync();

        var scope = factory.Services.CreateAsyncScope();
        return new OtpSendTestContext(
            factory.CreateClient(),
            factory.SmsSender,
            factory.OtpEmailSender,
            factory.CaptchaVerifier,
            scope);
    }
}
