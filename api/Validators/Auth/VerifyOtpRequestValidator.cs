using Amanah.Api.Services.Auth;
using Amanah.Contracts.Requests.Auth;
using FluentValidation;

namespace Amanah.Api.Validators.Auth;

public sealed class VerifyOtpRequestValidator : AbstractValidator<VerifyOtpRequest>
{
    private static readonly HashSet<string> AllowedPurposes =
    [
        OtpPurposes.Signup,
        OtpPurposes.PasswordReset,
    ];

    public VerifyOtpRequestValidator()
    {
        RuleFor(request => request.Phone)
            .NotEmpty()
            .WithMessage("Phone number is required.")
            .Must(phone => PhoneNormalizer.TryNormalize(phone, out _))
            .WithMessage("Phone number format is not valid.");

        RuleFor(request => request.Code)
            .NotEmpty()
            .WithMessage("Verification code is required.")
            .Must(code => OtpCodeNormalizer.TryNormalize(code, out _))
            .WithMessage("Verification code format is not valid.");

        RuleFor(request => request.Purpose)
            .NotEmpty()
            .WithMessage("OTP purpose is required.")
            .Must(AllowedPurposes.Contains)
            .WithMessage("OTP purpose is not valid.");
    }
}
