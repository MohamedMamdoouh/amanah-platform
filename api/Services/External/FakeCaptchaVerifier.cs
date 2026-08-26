using Amanah.Contracts.Errors;
using Amanah.Api.Models.Errors;

namespace Amanah.Api.Services.External;

public sealed class FakeCaptchaVerifier : ICaptchaVerifier
{
    public bool ShouldSucceed { get; set; } = true;

    public Task<Result> VerifyAsync(string token, CancellationToken cancellationToken = default)
    {
        if (ShouldSucceed && !string.IsNullOrWhiteSpace(token))
        {
            return Task.FromResult(Result.Ok());
        }

        Result result = ResultError.BadRequest(
            "CAPTCHA verification failed.",
            ErrorCodes.CaptchaFailed);

        return Task.FromResult(result);
    }
}
