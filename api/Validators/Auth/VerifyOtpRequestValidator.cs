using Amanah.Api.Models.Auth;
using Amanah.Api.Services.Auth;
using FluentValidation;

namespace Amanah.Api.Validators.Auth;

public sealed class VerifyOtpRequestValidator : AbstractValidator<VerifyOtpRequest>
{
    public VerifyOtpRequestValidator()
    {
        RuleFor(request => request.Phone)
            .NotEmpty()
            .WithMessage("Phone number is required.")
            .Must(phone => PhoneNormalizer.TryNormalize(phone, out _))
            .WithMessage("The phone number format is not accepted.");

        RuleFor(request => request.Code)
            .NotEmpty()
            .WithMessage("OTP code is required.")
            .Must(code => OtpCodeNormalizer.TryNormalize(code, out _))
            .WithMessage("The OTP code format is not accepted.");
    }
}
