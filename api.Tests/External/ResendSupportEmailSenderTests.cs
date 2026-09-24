using System.Net;
using System.Text.Json;
using Amanah.Api.Options;
using Amanah.Api.Services.External;
using Microsoft.Extensions.Logging.Abstractions;

namespace Amanah.Api.Tests.External;

public class ResendSupportEmailSenderTests
{
    private const string TestApiKey = "re_test_key";
    private const string TestFrom = "Amanah <test@example.com>";
    private const string TestAdmin = "admin@example.com";
    private const string TestReplyEmail = "user@example.com";
    private const string TestDisplayName = "Ahmad";
    private const string TestMessage = "I need help with a listing please.";

    [Fact]
    public async Task SendSupportMessageAsync_sends_reply_to_field_resend_accepts()
    {
        string? body = null;
        var handler = new StubHttpMessageHandler(async (request, cancellationToken) =>
        {
            body = await request.Content!.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK);
        });

        var sender = CreateSender(handler, TestAdmin);

        await sender.SendSupportMessageAsync(TestReplyEmail, TestDisplayName, TestMessage);

        Assert.NotNull(body);
        using var document = JsonDocument.Parse(body);
        Assert.Equal(TestReplyEmail, document.RootElement.GetProperty("reply_to").GetString());
        Assert.False(document.RootElement.TryGetProperty("replyTo", out _));
        Assert.Equal(TestAdmin, document.RootElement.GetProperty("to")[0].GetString());
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

        var exception = await Assert.ThrowsAsync<ResendApiException>(() =>
            sender.SendSupportMessageAsync(TestReplyEmail, TestDisplayName, TestMessage));

        Assert.Equal((int)HttpStatusCode.ServiceUnavailable, exception.StatusCodeValue);
        Assert.False(called);
    }

    private static ResendSupportEmailSender CreateSender(HttpMessageHandler handler, string? adminAlertTo)
    {
        var httpClient = new HttpClient(handler);
        var emailOptions = Microsoft.Extensions.Options.Options.Create(new EmailOptions
        {
            ApiKey = TestApiKey,
            FromAddress = TestFrom,
            AdminAlertTo = adminAlertTo,
        });
        return new ResendSupportEmailSender(
            httpClient,
            emailOptions,
            NullLogger<ResendSupportEmailSender>.Instance);
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
