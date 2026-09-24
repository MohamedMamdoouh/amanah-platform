using Amanah.Api.Models.Errors;
using Amanah.Api.Options;
using Amanah.Api.Services.Auth;
using Amanah.Api.Services.External;
using Amanah.Api.Utilities.Auth;
using Amanah.Api.Utilities.Common;
using Amanah.Api.Validators.Support;
using Amanah.Contracts.Errors;
using Amanah.Contracts.Requests.Support;
using Microsoft.Extensions.Options;

namespace Amanah.Api.Services.Support;

public sealed class SupportService(
    ICaptchaVerifier captchaVerifier,
    ISupportEmailSender supportEmailSender,
    IOptions<EmailOptions> emailOptions,
    IHostEnvironment hostEnvironment)
{
    public async Task<Result> SubmitAsync(
        SubmitSupportMessageRequest request,
        CancellationToken cancellationToken = default)
    {
        var captchaResult = await captchaVerifier.VerifyAsync(request.CaptchaToken, cancellationToken);
        if (!captchaResult.IsSuccess)
        {
            return captchaResult;
        }

        if (!EmailNormalizer.TryNormalize(request.ReplyEmail, out var normalizedReplyEmail))
        {
            return ResultError.BadRequest(
                "Email format is not valid.",
                ErrorCodes.FieldReplyEmailInvalid,
                new Dictionary<string, string[]>
                {
                    ["replyEmail"] = ["Email format is not valid."],
                });
        }

        if (!DisplayNameValidator.IsValid(request.DisplayName))
        {
            return ResultError.BadRequest(
                "Display name must be 3 to 40 characters using letters, numbers, spaces, or - _ .",
                ErrorCodes.FieldDisplayNameInvalid,
                new Dictionary<string, string[]>
                {
                    ["displayName"] = ["Display name must be 3 to 40 characters using letters, numbers, spaces, or - _ ."],
                });
        }

        var message = TextNormalizer.Normalize(request.Message);
        if (message.Length is < SubmitSupportMessageRequestValidator.MessageMinLength
            or > SubmitSupportMessageRequestValidator.MessageMaxLength)
        {
            return ResultError.BadRequest(
                "Message length is not valid.",
                ErrorCodes.FieldSupportMessageInvalid,
                new Dictionary<string, string[]>
                {
                    ["message"] = ["Message length is not valid."],
                });
        }

        if (!hostEnvironment.IsDevelopment() && !emailOptions.Value.IsConfigured)
        {
            return ResultError.ServiceUnavailable(
                "Support messaging is temporarily unavailable. Try again later or email us directly.",
                ErrorCodes.EmailUnavailable);
        }

        var displayName = TextNormalizer.Normalize(request.DisplayName);

        try
        {
            await supportEmailSender.SendSupportMessageAsync(
                normalizedReplyEmail,
                displayName,
                message,
                cancellationToken);
        }
        catch (ResendApiException)
        {
            return ResultError.ServiceUnavailable(
                "Support messaging is temporarily unavailable. Try again later or email us directly.",
                ErrorCodes.EmailUnavailable);
        }

        return Result.Ok();
    }
}
