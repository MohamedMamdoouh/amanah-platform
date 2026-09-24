using System.Net;
using Amanah.Api.Models.Errors;
using Microsoft.AspNetCore.Http;
using Amanah.Api.Options;
using Amanah.Api.Services.External;
using Amanah.Api.Services.Support;
using Amanah.Contracts.Errors;
using Amanah.Contracts.Requests.Support;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;

namespace Amanah.Api.Tests.Support;

public class SupportServiceTests
{
    [Fact]
    public async Task SubmitAsync_does_not_report_success_when_admin_inbox_is_missing()
    {
        var emailOptions = Microsoft.Extensions.Options.Options.Create(new EmailOptions
        {
            ApiKey = "xkeysib-test-key",
            FromAddress = "test@example.com",
            FromName = "Amanah",
            AdminAlertTo = "",
        });
        var sender = new BrevoSupportEmailSender(
            new HttpClient(new StubHttpMessageHandler()),
            emailOptions,
            NullLogger<BrevoSupportEmailSender>.Instance);
        var service = new SupportService(
            new SucceedingCaptchaVerifier(),
            sender,
            emailOptions,
            new ProductionEnvironment());

        var result = await service.SubmitAsync(new SubmitSupportMessageRequest
        {
            DisplayName = "Ahmad",
            ReplyEmail = "user@example.com",
            Message = "I need help with a listing please.",
            CaptchaToken = "token",
        });

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status503ServiceUnavailable, result.Error!.StatusCode);
        Assert.Equal(ErrorCodes.EmailUnavailable, result.Error.Code);
    }

    private sealed class SucceedingCaptchaVerifier : ICaptchaVerifier
    {
        public Task<Result> VerifyAsync(string token, CancellationToken cancellationToken = default) =>
            Task.FromResult(Result.Ok());
    }

    private sealed class ProductionEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Production;

        public string ApplicationName { get; set; } = "Amanah.Api.Tests";

        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;

        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    private sealed class StubHttpMessageHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
    }
}
