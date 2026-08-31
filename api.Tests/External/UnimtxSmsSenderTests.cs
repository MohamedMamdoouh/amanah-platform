using Amanah.Api.Options;
using Amanah.Api.Services.External;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using UniSdk;

namespace Amanah.Api.Tests.External;

public class UnimtxSmsSenderTests
{
    private const string TestPhone = "+201012345678";
    private const string TestCode = "123456";
    private static readonly Guid IdempotencyKey = Guid.Parse("11111111-1111-1111-1111-111111111111");

    [Fact]
    public async Task SendOtpAsync_sends_template_message_with_expected_payload()
    {
        var client = new FakeUnimtxClient();
        var sender = CreateSender(client);

        await sender.SendOtpAsync(TestPhone, TestCode, IdempotencyKey);

        Assert.NotNull(client.LastRequest);
        var request = client.LastRequest!;
        var requestType = request.GetType();
        Assert.Equal(TestPhone, requestType.GetProperty("to")!.GetValue(request));
        Assert.Equal("pub_verif_en_ttl", requestType.GetProperty("templateId")!.GetValue(request));

        var templateData = requestType.GetProperty("templateData")!.GetValue(request)!;
        var templateDataType = templateData.GetType();
        Assert.Equal(TestCode, templateDataType.GetProperty("code")!.GetValue(templateData));
        Assert.Equal("10", templateDataType.GetProperty("ttl")!.GetValue(templateData));
    }

    [Fact]
    public async Task SendOtpAsync_succeeds_when_sdk_returns_response()
    {
        var sender = CreateSender(new FakeUnimtxClient());

        var exception = await Record.ExceptionAsync(() =>
            sender.SendOtpAsync(TestPhone, TestCode, IdempotencyKey));

        Assert.Null(exception);
    }

    [Fact]
    public async Task SendOtpAsync_throws_when_sdk_raises_unimtx_error()
    {
        var sender = CreateSender(new FakeUnimtxClient
        {
            ExceptionToThrow = new UniException("InsufficientFunds", "105400"),
        });

        var exception = await Assert.ThrowsAsync<HttpRequestException>(() =>
            sender.SendOtpAsync(TestPhone, TestCode, IdempotencyKey));

        Assert.Contains("105400", exception.Message);
        Assert.Contains("InsufficientFunds", exception.Message);
    }

    private static UnimtxSmsSender CreateSender(FakeUnimtxClient client)
    {
        var otpOptions = Microsoft.Extensions.Options.Options.Create(new OtpOptions { CodeLifetimeMinutes = 10 });
        return new UnimtxSmsSender(
            client,
            otpOptions,
            NullLogger<UnimtxSmsSender>.Instance);
    }

    private sealed class FakeUnimtxClient : IUnimtxClient
    {
        public object? LastRequest { get; private set; }

        public UniException? ExceptionToThrow { get; init; }

        public Task SendMessageAsync(
            object request,
            CancellationToken cancellationToken = default)
        {
            LastRequest = request;
            cancellationToken.ThrowIfCancellationRequested();

            if (ExceptionToThrow is not null)
            {
                throw ExceptionToThrow;
            }

            return Task.CompletedTask;
        }
    }
}
