using System.Net;
using Amanah.Api.Options;
using Amanah.Api.Services.External;
using Microsoft.Extensions.Logging.Abstractions;

namespace Amanah.Api.Tests.External;

public class ResendOtpEmailSenderTests
{
    private const string TestApiKey = "re_test_key";
    private const string TestFrom = "Amanah <test@example.com>";
    private const string TestEmail = "user@example.com";
    private const string TestCode = "123456";
    private static readonly Guid IdempotencyKey = Guid.Parse("11111111-1111-1111-1111-111111111111");

    [Fact]
    public async Task SendOtpAsync_sends_idempotency_key_header()
    {
        HttpRequestMessage? capturedRequest = null;
        var handler = new StubHttpMessageHandler((request, _) =>
        {
            capturedRequest = request;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        });

        var sender = CreateSender(handler);

        await sender.SendOtpAsync(TestEmail, TestCode, IdempotencyKey);

        Assert.NotNull(capturedRequest);
        Assert.True(capturedRequest!.Headers.TryGetValues("Idempotency-Key", out var values));
        Assert.Equal(IdempotencyKey.ToString(), Assert.Single(values));
    }

    private static ResendOtpEmailSender CreateSender(HttpMessageHandler handler)
    {
        var httpClient = new HttpClient(handler);
        var emailOptions = Microsoft.Extensions.Options.Options.Create(new EmailOptions
        {
            ApiKey = TestApiKey,
            FromAddress = TestFrom,
        });
        var otpOptions = Microsoft.Extensions.Options.Options.Create(new OtpOptions());
        return new ResendOtpEmailSender(
            httpClient,
            emailOptions,
            otpOptions,
            NullLogger<ResendOtpEmailSender>.Instance);
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
