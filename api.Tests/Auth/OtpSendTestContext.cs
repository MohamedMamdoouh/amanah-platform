using System.Net.Http.Headers;
using System.Net.Http.Json;
using Amanah.Api.Auth;
using Amanah.Api.Models.Common;
using Amanah.Api.Data;
using Amanah.Api.Data.Entities;
using Amanah.Contracts.Requests.Auth;
using Amanah.Contracts.Responses.Auth;
using Amanah.Api.Services.Auth;
using Amanah.Api.Services.External;
using Amanah.Api.Tests.Auth.Fakes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Amanah.Api.Tests.Auth;

public sealed class OtpSendTestContext : IAsyncDisposable
{
    private readonly AsyncServiceScope _scope;

    public OtpSendTestContext(
        HttpClient client,
        RecordingSmsSender smsSender,
        RecordingOtpEmailSender emailSender,
        FakeCaptchaVerifier captchaVerifier,
        AsyncServiceScope scope)
    {
        Client = client;
        SmsSender = smsSender;
        EmailSender = emailSender;
        CaptchaVerifier = captchaVerifier;
        _scope = scope;
        DbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        PasswordHasher = scope.ServiceProvider.GetRequiredService<UserPasswordHasher>();
    }

    public HttpClient Client { get; }

    public RecordingSmsSender SmsSender { get; }

    public RecordingOtpEmailSender EmailSender { get; }

    public FakeCaptchaVerifier CaptchaVerifier { get; }

    public AppDbContext DbContext { get; }

    public UserPasswordHasher PasswordHasher { get; }

    public OtpSmsOutboxDispatcher Dispatcher =>
        _scope.ServiceProvider.GetRequiredService<OtpSmsOutboxDispatcher>();

    public OtpEmailOutboxDispatcher EmailDispatcher =>
        _scope.ServiceProvider.GetRequiredService<OtpEmailOutboxDispatcher>();

    public async Task<HttpResponseMessage> SendOtpAsync(
        string identifier,
        string purpose = OtpPurposes.Signup,
        string captchaToken = "valid-token",
        string channel = AuthIdentifierChannels.Phone)
    {
        return await Client.PostAsJsonAsync("/api/v1/auth/otp/send", new
        {
            channel,
            identifier,
            captchaToken,
            purpose,
        });
    }

    public async Task<(HttpResponseMessage Response, VerifyOtpResponse? Body)> VerifyOtpAsync(
        string identifier,
        string code,
        string purpose = OtpPurposes.Signup,
        string channel = AuthIdentifierChannels.Phone)
    {
        var response = await Client.PostAsJsonAsync("/api/v1/auth/otp/verify", new
        {
            channel,
            identifier,
            code,
            purpose,
        });

        VerifyOtpResponse? body = response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<VerifyOtpResponse>(ApiJson.SerializerOptions)
            : null;

        return (response, body);
    }

    public async Task<string> SendOtpAndGetCodeAsync(
        string phone,
        string purpose = OtpPurposes.Signup)
    {
        var initialCount = SmsSender.SentMessages.Count;
        var response = await SendOtpAsync(phone, purpose);
        if (response.StatusCode != System.Net.HttpStatusCode.NoContent)
        {
            throw new InvalidOperationException(
                $"OTP send failed with status {response.StatusCode}.");
        }

        await WaitForSmsCountAsync(initialCount + 1);
        return SmsSender.SentMessages[^1].Code;
    }

    public async Task<(HttpResponseMessage Response, AuthSessionResponse? Body)> RegisterAsync(
        string signupToken,
        string displayName = "Ahmed",
        string password = TestAuthHelpers.DefaultPassword,
        bool acceptTerms = true)
    {
        var response = await Client.PostAsJsonAsync("/api/v1/auth/register", new
        {
            signupToken,
            displayName,
            password,
            acceptTerms,
        });

        AuthSessionResponse? body = response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<AuthSessionResponse>()
            : null;

        return (response, body);
    }

    public async Task<(HttpResponseMessage Response, AuthSessionResponse? Body)> LoginAsync(
        string identifier,
        string password = TestAuthHelpers.DefaultPassword,
        string channel = AuthIdentifierChannels.Phone)
    {
        var response = await Client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            channel,
            identifier,
            password,
        });

        AuthSessionResponse? body = response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<AuthSessionResponse>()
            : null;

        return (response, body);
    }

    public async Task<(HttpResponseMessage Response, AuthSessionResponse? Body)> ResetPasswordAsync(
        string resetToken,
        string password = "NewPass123")
    {
        var response = await Client.PostAsJsonAsync("/api/v1/auth/password/reset", new
        {
            resetToken,
            password,
        });

        AuthSessionResponse? body = response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<AuthSessionResponse>()
            : null;

        return (response, body);
    }

    public async Task<(HttpResponseMessage Response, AuthSessionResponse? Body)> RefreshAsync(
        string? refreshToken = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/refresh");
        if (refreshToken is not null)
        {
            request.Headers.Add(
                "Cookie",
                $"{RefreshTokenCookieManager.CookieName}={refreshToken}");
        }

        var response = await Client.SendAsync(request);

        AuthSessionResponse? body = response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<AuthSessionResponse>()
            : null;

        return (response, body);
    }

    public async Task<HttpResponseMessage> LogoutAsync(string accessToken)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/logout");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return await Client.SendAsync(request);
    }

    public async Task<HttpResponseMessage> LogoutEverywhereAsync(string accessToken)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/logout-everywhere");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return await Client.SendAsync(request);
    }

    public async Task<(HttpResponseMessage Response, UserProfileResponse? Body)> GetMeAsync(
        string? accessToken = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/auth/me");
        if (accessToken is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        }

        var response = await Client.SendAsync(request);
        UserProfileResponse? body = response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<UserProfileResponse>()
            : null;

        return (response, body);
    }

    public async Task<(AuthSessionResponse Session, string Phone)> RegisterNewUserAsync(
        string phone = "01012345678",
        string password = TestAuthHelpers.DefaultPassword)
    {
        var code = await SendOtpAndGetCodeAsync(phone);
        var (_, verifyBody) = await VerifyOtpAsync(phone, code);
        var (registerResponse, session) = await RegisterAsync(
            verifyBody!.SignupToken!,
            "Ahmed",
            password);

        if (registerResponse.StatusCode != System.Net.HttpStatusCode.OK || session is null)
        {
            throw new InvalidOperationException($"Register failed: {registerResponse.StatusCode}");
        }

        return (session, phone);
    }

    public static string? ExtractRefreshToken(HttpResponseMessage response)
    {
        if (!response.Headers.TryGetValues("Set-Cookie", out var cookies))
        {
            return null;
        }

        foreach (var cookie in cookies)
        {
            var prefix = $"{RefreshTokenCookieManager.CookieName}=";
            if (!cookie.StartsWith(prefix, StringComparison.Ordinal))
            {
                continue;
            }

            var valuePart = cookie.Split(';', 2)[0];
            return valuePart[prefix.Length..];
        }

        return null;
    }

    public static void AssertRefreshCookieSet(HttpResponseMessage response)
    {
        Assert.NotNull(ExtractRefreshToken(response));
    }

    public async Task<string> SendOtpAndGetEmailCodeAsync(
        string email,
        string purpose = OtpPurposes.Signup)
    {
        var initialCount = EmailSender.SentMessages.Count;
        var response = await SendOtpAsync(
            email,
            purpose,
            channel: AuthIdentifierChannels.Email);
        if (response.StatusCode != System.Net.HttpStatusCode.NoContent)
        {
            throw new InvalidOperationException(
                $"OTP send failed with status {response.StatusCode}.");
        }

        await WaitForEmailCountAsync(initialCount + 1);
        return EmailSender.SentMessages[^1].Code;
    }

    public async Task WaitForEmailCountAsync(int expectedCount, TimeSpan? timeout = null)
    {
        timeout ??= TimeSpan.FromSeconds(5);
        var deadline = DateTime.UtcNow.Add(timeout.Value);

        while (DateTime.UtcNow < deadline)
        {
            if (EmailSender.SentMessages.Count == expectedCount)
            {
                return;
            }

            await DispatchPendingEmailOutboxMessagesAsync();
            await Task.Delay(100);
        }

        throw new TimeoutException(
            $"Expected {expectedCount} email message(s), but found {EmailSender.SentMessages.Count}.");
    }

    public async Task WaitForSmsCountAsync(int expectedCount, TimeSpan? timeout = null)
    {
        timeout ??= TimeSpan.FromSeconds(5);
        var deadline = DateTime.UtcNow.Add(timeout.Value);

        while (DateTime.UtcNow < deadline)
        {
            if (SmsSender.SentMessages.Count == expectedCount)
            {
                return;
            }

            await DispatchPendingOutboxMessagesAsync();
            await Task.Delay(100);
        }

        throw new TimeoutException(
            $"Expected {expectedCount} SMS message(s), but found {SmsSender.SentMessages.Count}.");
    }

    public async Task<OtpSmsOutboxStatus> WaitForOutboxStatusAsync(
        OtpSmsOutboxStatus expectedStatus,
        TimeSpan? timeout = null)
    {
        timeout ??= TimeSpan.FromSeconds(5);
        var deadline = DateTime.UtcNow.Add(timeout.Value);

        while (DateTime.UtcNow < deadline)
        {
            DbContext.ChangeTracker.Clear();
            var status = await DbContext.OtpSmsOutboxMessages
                .Select(message => message.Status)
                .SingleOrDefaultAsync();

            if (status == expectedStatus)
            {
                return status;
            }

            await DispatchPendingOutboxMessagesAsync();
            await Task.Delay(100);
        }

        DbContext.ChangeTracker.Clear();
        var actualStatus = await DbContext.OtpSmsOutboxMessages
            .Select(message => message.Status)
            .SingleOrDefaultAsync();

        throw new TimeoutException(
            $"Expected outbox status {expectedStatus}, but found {actualStatus}.");
    }

    private async Task DispatchPendingEmailOutboxMessagesAsync()
    {
        DbContext.ChangeTracker.Clear();
        var pendingIds = await DbContext.OtpEmailOutboxMessages
            .Where(message => message.Status == OtpSmsOutboxStatus.Pending)
            .Select(message => message.Id)
            .ToListAsync();

        foreach (var outboxId in pendingIds)
        {
            await EmailDispatcher.DispatchAsync(outboxId);
        }
    }

    private async Task DispatchPendingOutboxMessagesAsync()
    {
        DbContext.ChangeTracker.Clear();
        var pendingIds = await DbContext.OtpSmsOutboxMessages
            .Where(message => message.Status == OtpSmsOutboxStatus.Pending)
            .Select(message => message.Id)
            .ToListAsync();

        foreach (var outboxId in pendingIds)
        {
            await Dispatcher.DispatchAsync(outboxId);
        }
    }

    public async ValueTask DisposeAsync()
    {
        await DbContext.DisposeAsync();
        await _scope.DisposeAsync();
    }
}
