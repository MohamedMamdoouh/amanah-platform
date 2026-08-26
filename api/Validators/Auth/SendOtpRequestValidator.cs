using Amanah.Api.Services.Auth;
using Amanah.Contracts.Errors;
using Amanah.Contracts.Requests.Auth;
using FluentValidation;

namespace Amanah.Api.Validators.Auth;

public sealed class SendOtpRequestValidator : AbstractValidator<SendOtpRequest>
{
    public SendOtpRequestValidator()
    {
        RuleFor(request => request.Phone)
            .NotEmpty()
            .WithErrorCode(ErrorCodes.FieldPhoneRequired)
            .WithMessage("Phone number is required.")
            .Must(phone => PhoneNormalizer.TryNormalize(phone, out _))
            .WithErrorCode(ErrorCodes.FieldPhoneInvalid)
            .WithMessage("Phone number format is not valid.");

        RuleFor(request => request.CaptchaToken)
            .NotEmpty()
            .WithErrorCode(ErrorCodes.FieldCaptchaTokenRequired)
            .WithMessage("Captcha verification is required.");
    }
}
