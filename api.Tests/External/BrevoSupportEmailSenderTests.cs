using System.Net;
using System.Text.Json;
using Amanah.Api.Options;
using Amanah.Api.Services.External;
using Amanah.Api.Services.External.Email;
using Microsoft.Extensions.Logging.Abstractions;

namespace Amanah.Api.Tests.External;

public class BrevoSupportEmailSenderTests
{
    private const string TestApiKey = "xkeysib-test-key";
    private const string TestFrom = "test@example.com";
    private const string TestFromName = "Amanah";
    private const string TestAdmin = "admin@example.com";
    private const string TestReplyEmail = "user@example.com";
    private const string TestDisplayName = "Ahmad";
    private const string TestMessage = "I need help with a listing please.";

    [Fact]
    public async Task SendSupportMessageAsync_sends_reply_to_field_brevo_accepts()
    {
        HttpRequestMessage? capturedRequest = null;
        string? body = null;
        var handler = new StubHttpMessageHandler(async (request, cancellationToken) =>
        {
            capturedRequest = request;
            body = await request.Content!.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.Created);
        });

        var sender = CreateSender(handler, TestAdmin);

        await sender.SendSupportMessageAsync(TestReplyEmail, TestDisplayName, TestMessage);

        Assert.NotNull(capturedRequest);
        Assert.Equal(BrevoTransactionalEmail.ApiUrl, capturedRequest!.RequestUri!.ToString());
        Assert.True(capturedRequest.Headers.TryGetValues("api-key", out var apiKeyValues));
        Assert.Equal(TestApiKey, Assert.Single(apiKeyValues));

        Assert.NotNull(body);
        using var document = JsonDocument.Parse(body);
        var root = document.RootElement;
        Assert.Equal(TestReplyEmail, root.GetProperty("replyTo").GetProperty("email").GetString());
        Assert.False(root.TryGetProperty("reply_to", out _));
        Assert.Equal(TestAdmin, root.GetProperty("to")[0].GetProperty("email").GetString());
        Assert.Equal(TestFrom, root.GetProperty("sender").GetProperty("email").GetString());
        Assert.Equal(TestFromName, root.GetProperty("sender").GetProperty("name").GetString());
        Assert.True(root.TryGetProperty("htmlContent", out _));
        Assert.True(root.TryGetProperty("textContent", out _));
    }

    [Fact]
    public async Task SendSupportMessageAsync_fails_when_admin_inbox_is_missing()
    {
        var called = false;
        var handler = new StubHttpMessageHandler((_, _) =>
        {
            called = true;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        });

        var sender = CreateSender(handler, adminAlertTo: " ");

        var exception = await Assert.ThrowsAsync<EmailApiException>(() =>
            sender.SendSupportMessageAsync(TestReplyEmail, TestDisplayName, TestMessage));

        Assert.Equal((int)HttpStatusCode.ServiceUnavailable, exception.StatusCodeValue);
        Assert.False(called);
    }

    private static BrevoSupportEmailSender CreateSender(HttpMessageHandler handler, string? adminAlertTo)
    {
        var httpClient = new HttpClient(handler);
        var emailOptions = Microsoft.Extensions.Options.Options.Create(new EmailOptions
        {
            ApiKey = TestApiKey,
            FromAddress = TestFrom,
            FromName = TestFromName,
            AdminAlertTo = adminAlertTo,
        });
        return new BrevoSupportEmailSender(
            httpClient,
            emailOptions,
            NullLogger<BrevoSupportEmailSender>.Instance);
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
