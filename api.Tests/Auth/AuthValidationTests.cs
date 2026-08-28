using Amanah.Api.Data;
using Amanah.Api.Tests.Infrastructure;
using Amanah.Contracts.Errors;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Amanah.Api.Tests.Auth;

public class AuthValidationTests(ApiWebApplicationFactory factory) : IClassFixture<ApiWebApplicationFactory>
{
    [Fact]
    public async Task SendOtp_with_empty_phone_returns_api_error_with_field_codes()
    {
        await using var context = await CreateContextAsync();

        var response = await context.SendOtpAsync(string.Empty);
        var error = await context.ReadErrorAsync(response);

        Assert.Equal(System.Net.HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(ErrorCodes.ValidationFailed, error?.Code);
        Assert.Contains("Phone number is required.", error?.Errors?["phone"] ?? []);
    }

    [Fact]
    public async Task Refresh_without_cookie_returns_refresh_invalid()
    {
        await using var context = await CreateContextAsync();

        var (response, body) = await context.RefreshAsync();
        var error = await context.ReadErrorAsync(response);

        Assert.Equal(System.Net.HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(ErrorCodes.RefreshInvalid, error?.Code);
        Assert.Null(body);
    }

    [Fact]
    public async Task Register_with_accept_terms_false_returns_field_error_code()
    {
        await using var context = await CreateContextAsync();

        var code = await context.SendOtpAndGetCodeAsync("01012345678");
        var (_, verifyBody) = await context.VerifyOtpAsync("01012345678", code);
        var (response, session) = await context.RegisterAsync(
            verifyBody!.SignupToken!,
            "Ahmed",
            acceptTerms: false);
        var error = await context.ReadErrorAsync(response);

        Assert.Equal(System.Net.HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(ErrorCodes.ValidationFailed, error?.Code);
        Assert.Contains(
            "You must accept the terms and conditions and privacy policy.",
            error?.Errors?["acceptTerms"] ?? []);
        Assert.Null(session);
    }

    [Fact]
    public async Task Logout_without_cookie_returns_refresh_invalid()
    {
        await using var context = await CreateContextAsync();

        var (session, _) = await context.RegisterNewUserAsync();
        var clientWithoutCookies = factory.CreateClient();
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/logout");
        request.Headers.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", session.AccessToken);
        var response = await clientWithoutCookies.SendAsync(request);
        var error = await context.ReadErrorAsync(response);

        Assert.Equal(System.Net.HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(ErrorCodes.RefreshInvalid, error?.Code);
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

        var scope = factory.Services.CreateAsyncScope();
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions
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
