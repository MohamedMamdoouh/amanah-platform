using Amanah.Api.Services.Auth;
using Amanah.Api.Utilities.Auth;
using Amanah.Contracts.Requests.Support;
using FluentValidation;

namespace Amanah.Api.Validators.Support;

public sealed class SubmitSupportMessageRequestValidator : AbstractValidator<SubmitSupportMessageRequest>
{
    public const int MessageMinLength = 10;
    public const int MessageMaxLength = 2000;

    public SubmitSupportMessageRequestValidator()
    {
        RuleFor(request => request.DisplayName)
            .NotEmpty()
            .WithMessage("Display name is required.")
            .Must(DisplayNameValidator.IsValid)
            .WithMessage("Display name must be 3 to 40 characters using letters, numbers, spaces, or - _ .");

        RuleFor(request => request.ReplyEmail)
            .NotEmpty()
            .WithMessage("Reply email is required.")
            .Must(email => EmailNormalizer.TryNormalize(email, out _))
            .WithMessage("Email format is not valid.");

        RuleFor(request => request.Message)
            .NotEmpty()
            .WithMessage("Message is required.")
            .MinimumLength(MessageMinLength)
            .WithMessage($"Message must be at least {MessageMinLength} characters.")
            .MaximumLength(MessageMaxLength)
            .WithMessage($"Message must be at most {MessageMaxLength} characters.");

        RuleFor(request => request.CaptchaToken)
            .NotEmpty()
            .WithMessage("Captcha verification is required.");
    }
}
