using Amanah.Api.Services.Auth;
using Amanah.Contracts.Requests.Auth;
using FluentValidation;

namespace Amanah.Api.Validators.Auth;

public sealed class SendOtpRequestValidator : AbstractValidator<SendOtpRequest>
{
    private static readonly HashSet<string> AllowedPurposes =
    [
        OtpPurposes.Signup,
        OtpPurposes.PasswordReset,
    ];

    public SendOtpRequestValidator()
    {
        RuleFor(request => request.Phone)
            .NotEmpty()
            .WithMessage("Phone number is required.")
            .Must(phone => PhoneNormalizer.TryNormalize(phone, out _))
            .WithMessage("Phone number format is not valid.");

        RuleFor(request => request.CaptchaToken)
            .NotEmpty()
            .WithMessage("Captcha verification is required.");

        RuleFor(request => request.Purpose)
            .NotEmpty()
            .WithMessage("OTP purpose is required.")
            .Must(AllowedPurposes.Contains)
            .WithMessage("OTP purpose is not valid.");
    }
}
