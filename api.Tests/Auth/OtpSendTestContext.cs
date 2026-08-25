using System.Net.Http.Json;
using Amanah.Api.Data;
using Amanah.Api.Data.Entities;
using Amanah.Api.Models.Auth;
using Amanah.Api.Models.Errors;
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
