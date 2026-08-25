using Amanah.Api.Models.Common;
using Amanah.Api.Models.Errors;
using Amanah.Api.Models.Results;
using Amanah.Api.Options;
using Microsoft.Extensions.Options;

namespace Amanah.Api.Services.External;

public sealed class TurnstileCaptchaVerifier(
    HttpClient httpClient,
    IOptions<TurnstileOptions> options) : ICaptchaVerifier
{
    private static readonly string _cloudflareTurnstileUrl
        = "https://challenges.cloudflare.com/turnstile/v0/siteverify";

    public async Task<Result> VerifyAsync(string token, CancellationToken cancellationToken = default)
    {
        var secretKey = options.Value.SecretKey;
        if (string.IsNullOrWhiteSpace(secretKey) || string.IsNullOrWhiteSpace(token))
        {
            return ResultError.BadRequest(
                "CAPTCHA verification failed.",
                ErrorCodes.CaptchaFailed);
        }

        try
        {
            using var content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["secret"] = secretKey,
                ["response"] = token,
            });

            using var response = await httpClient.PostAsync(
                _cloudflareTurnstileUrl,
                content,
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                return ResultError.BadRequest(
                    "CAPTCHA verification failed.",
                    ErrorCodes.CaptchaFailed);
            }

            var result = await response.Content.ReadFromJsonAsync<TurnstileVerifyResponse>(
                ApiJson.SerializerOptions,
                cancellationToken);

            return result?.Success == true
                ? Result.Ok()
                : ResultError.BadRequest(
                    "CAPTCHA verification failed.",
                    ErrorCodes.CaptchaFailed);
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return ServiceUnavailable();
        }
        catch (TimeoutException)
        {
            return ServiceUnavailable();
        }
        catch (HttpRequestException)
        {
            return ResultError.BadRequest(
                "CAPTCHA verification failed.",
                ErrorCodes.CaptchaFailed);
        }
    }

    private static ResultError ServiceUnavailable() =>
        ResultError.ServiceUnavailable(
            "Service is temporarily unavailable. Please try again later.");

    private sealed class TurnstileVerifyResponse
    {
        public bool Success { get; init; }
    }
}
