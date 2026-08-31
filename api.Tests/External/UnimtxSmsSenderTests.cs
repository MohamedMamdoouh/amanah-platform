using System.Net;
using System.Text.Json;
using Amanah.Api.Options;
using Amanah.Api.Services.External;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Amanah.Api.Tests.External;

public class UnimtxSmsSenderTests
{
    private const string TestApiKey = "test-access-key-id";
    private const string TestPhone = "+201012345678";
    private const string TestCode = "123456";
    private static readonly Guid IdempotencyKey = Guid.Parse("11111111-1111-1111-1111-111111111111");

    [Fact]
    public async Task SendOtpAsync_sends_otp_send_request_with_expected_payload()
    {
        string? capturedBody = null;
        HttpRequestMessage? capturedRequest = null;
        var handler = new StubHttpMessageHandler(async (request, _) =>
        {
            capturedRequest = request;
            capturedBody = request.Content is null
                ? null
                : await request.Content.ReadAsStringAsync();
            return CreateJsonResponse(HttpStatusCode.OK, """{"code":"0","message":"Success"}""");
        });

        var sender = CreateSender(handler);

        await sender.SendOtpAsync(TestPhone, TestCode, IdempotencyKey);

        Assert.NotNull(capturedRequest);
        Assert.Equal(HttpMethod.Post, capturedRequest!.Method);
        Assert.Contains("action=otp.send", capturedRequest.RequestUri!.Query);
        Assert.Contains($"accessKeyId={TestApiKey}", capturedRequest.RequestUri.Query);

        Assert.NotNull(capturedBody);
        using var document = JsonDocument.Parse(capturedBody!);
        var root = document.RootElement;
        Assert.Equal(TestPhone, root.GetProperty("to").GetString());
        Assert.Equal(TestCode, root.GetProperty("code").GetString());
        Assert.Equal(6, root.GetProperty("digits").GetInt32());
        Assert.Equal(600, root.GetProperty("ttl").GetInt32());
        Assert.Equal("sms", root.GetProperty("channel").GetString());
    }

    [Fact]
    public async Task SendOtpAsync_succeeds_when_response_code_is_zero()
    {
        var handler = new StubHttpMessageHandler((_, _) =>
            Task.FromResult(CreateJsonResponse(HttpStatusCode.OK, """{"code":"0","message":"Success"}""")));

        var sender = CreateSender(handler);

        var exception = await Record.ExceptionAsync(() =>
            sender.SendOtpAsync(TestPhone, TestCode, IdempotencyKey));

        Assert.Null(exception);
    }

    [Fact]
    public async Task SendOtpAsync_throws_when_response_code_is_non_zero()
    {
        var handler = new StubHttpMessageHandler((_, _) =>
            Task.FromResult(CreateJsonResponse(
                HttpStatusCode.BadRequest,
                """{"code":"105400","message":"InsufficientFunds"}""")));

        var sender = CreateSender(handler);

        var exception = await Assert.ThrowsAsync<HttpRequestException>(() =>
            sender.SendOtpAsync(TestPhone, TestCode, IdempotencyKey));

        Assert.Contains("105400", exception.Message);
        Assert.Contains("InsufficientFunds", exception.Message);
    }

    private static UnimtxSmsSender CreateSender(HttpMessageHandler handler)
    {
        var httpClient = new HttpClient(handler);
        var smsOptions = Microsoft.Extensions.Options.Options.Create(new SmsOptions { ApiKey = TestApiKey });
        var otpOptions = Microsoft.Extensions.Options.Options.Create(new OtpOptions { CodeLifetimeMinutes = 10 });
        return new UnimtxSmsSender(
            httpClient,
            smsOptions,
            otpOptions,
            NullLogger<UnimtxSmsSender>.Instance);
    }

    private static HttpResponseMessage CreateJsonResponse(HttpStatusCode statusCode, string body) =>
        new(statusCode)
        {
            Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json"),
        };

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
