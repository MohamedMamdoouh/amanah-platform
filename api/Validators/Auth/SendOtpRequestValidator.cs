using Amanah.Api.Models.Auth;
using Amanah.Api.Services.Auth;
using FluentValidation;

namespace Amanah.Api.Validators.Auth;

public sealed class SendOtpRequestValidator : AbstractValidator<SendOtpRequest>
{
    public SendOtpRequestValidator()
    {
        RuleFor(request => request.Phone)
            .NotEmpty()
            .WithMessage("Phone number is required.")
            .Must(phone => PhoneNormalizer.TryNormalize(phone, out _))
            .WithMessage("The phone number format is not accepted.");

        RuleFor(request => request.CaptchaToken)
            .NotEmpty()
            .WithMessage("CAPTCHA token is required.");
    }
}
