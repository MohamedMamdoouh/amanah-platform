using System.Net.Http.Headers;
using System.Net.Http.Json;
using Amanah.Api.Auth;
using Amanah.Api.Data;
using Amanah.Api.Data.Entities;
using Amanah.Contracts.Responses.Auth;
using Amanah.Contracts.Errors;
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
        FakeCaptchaVerifier captchaVerifier,
        AsyncServiceScope scope)
    {
        Client = client;
        SmsSender = smsSender;
        CaptchaVerifier = captchaVerifier;
        _scope = scope;
        DbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    }

    public HttpClient Client { get; }

    public RecordingSmsSender SmsSender { get; }

    public FakeCaptchaVerifier CaptchaVerifier { get; }

    public AppDbContext DbContext { get; }

    public OtpSmsOutboxDispatcher Dispatcher =>
        _scope.ServiceProvider.GetRequiredService<OtpSmsOutboxDispatcher>();

    public async Task<HttpResponseMessage> SendOtpAsync(string phone, string captchaToken = "valid-token")
    {
        return await Client.PostAsJsonAsync("/api/v1/auth/otp/send", new
        {
            phone,
            captchaToken,
        });
    }

    public async Task<(HttpResponseMessage Response, VerifyOtpResponse? Body)> VerifyOtpAsync(
        string phone,
        string code)
    {
        var response = await Client.PostAsJsonAsync("/api/v1/auth/otp/verify", new
        {
            phone,
            code,
        });

        VerifyOtpResponse? body = response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<VerifyOtpResponse>()
            : null;

        return (response, body);
    }

    public async Task<string> SendOtpAndGetCodeAsync(string phone)
    {
        var response = await SendOtpAsync(phone);
        if (response.StatusCode != System.Net.HttpStatusCode.NoContent)
        {
            throw new InvalidOperationException(
                $"OTP send failed with status {response.StatusCode}.");
        }

        await WaitForSmsCountAsync(1);
        return SmsSender.SentMessages[^1].Code;
    }

    public async Task<(HttpResponseMessage Response, AuthSessionResponse? Body)> RegisterAsync(
        string signupToken,
        string displayName = "Ahmed",
        bool acceptTerms = true)
    {
        var response = await Client.PostAsJsonAsync("/api/v1/auth/register", new
        {
            signupToken,
            displayName,
            acceptTerms,
        });

        AuthSessionResponse? body = response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<AuthSessionResponse>()
            : null;

        return (response, body);
    }

    public async Task<(HttpResponseMessage Response, AuthSessionResponse? Body)> LoginAsync(
        string phone,
        string loginToken)
    {
        var response = await Client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            phone,
            loginToken,
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

    public async Task<(AuthSessionResponse Session, string Phone)> RegisterNewUserAsync(string phone = "01012345678")
    {
        var code = await SendOtpAndGetCodeAsync(phone);
        var (_, verifyBody) = await VerifyOtpAsync(phone, code);
        var (registerResponse, session) = await RegisterAsync(verifyBody!.SignupToken!, "Ahmed");

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

    public async Task<ApiError?> ReadErrorAsync(HttpResponseMessage response)
    {
        return await response.Content.ReadFromJsonAsync<ApiError>();
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
