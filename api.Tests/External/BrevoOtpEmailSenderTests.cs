using System.Net;
using System.Text.Json;
using Amanah.Api.Options;
using Amanah.Api.Services.External;
using Amanah.Api.Services.External.Email;
using Microsoft.Extensions.Logging.Abstractions;

namespace Amanah.Api.Tests.External;

public class BrevoOtpEmailSenderTests
{
    private const string TestApiKey = "xkeysib-test-key";
    private const string TestFrom = "test@example.com";
    private const string TestFromName = "Amanah";
    private const string TestEmail = "user@example.com";
    private const string TestCode = "123456";
    private static readonly Guid IdempotencyKey = Guid.Parse("11111111-1111-1111-1111-111111111111");

    [Fact]
    public async Task SendOtpAsync_sends_brevo_request_with_idempotency_key()
    {
        HttpRequestMessage? capturedRequest = null;
        string? body = null;
        var handler = new StubHttpMessageHandler(async (request, cancellationToken) =>
        {
            capturedRequest = request;
            body = await request.Content!.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.Created);
        });

        var sender = CreateSender(handler);

        await sender.SendOtpAsync(TestEmail, TestCode, IdempotencyKey);

        Assert.NotNull(capturedRequest);
        Assert.Equal(HttpMethod.Post, capturedRequest!.Method);
        Assert.Equal(BrevoTransactionalEmail.ApiUrl, capturedRequest.RequestUri!.ToString());
        Assert.True(capturedRequest.Headers.TryGetValues("api-key", out var apiKeyValues));
        Assert.Equal(TestApiKey, Assert.Single(apiKeyValues));
        Assert.False(capturedRequest.Headers.Contains("Authorization"));

        Assert.NotNull(body);
        using var document = JsonDocument.Parse(body);
        var root = document.RootElement;
        Assert.Equal(TestFrom, root.GetProperty("sender").GetProperty("email").GetString());
        Assert.Equal(TestFromName, root.GetProperty("sender").GetProperty("name").GetString());
        Assert.Equal(TestEmail, root.GetProperty("to")[0].GetProperty("email").GetString());
        Assert.True(root.TryGetProperty("htmlContent", out _));
        Assert.True(root.TryGetProperty("textContent", out _));
        Assert.Equal(
            IdempotencyKey.ToString(),
            root.GetProperty("headers").GetProperty("idempotencyKey").GetString());
    }

    private static BrevoOtpEmailSender CreateSender(HttpMessageHandler handler)
    {
        var httpClient = new HttpClient(handler);
        var emailOptions = Microsoft.Extensions.Options.Options.Create(new EmailOptions
        {
            ApiKey = TestApiKey,
            FromAddress = TestFrom,
            FromName = TestFromName,
        });
        var otpOptions = Microsoft.Extensions.Options.Options.Create(new OtpOptions());
        return new BrevoOtpEmailSender(
            httpClient,
            emailOptions,
            otpOptions,
            NullLogger<BrevoOtpEmailSender>.Instance);
    }

    private sealed class StubHttpMessageHandler(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            handler(request, cancellationToken);
    }
}
