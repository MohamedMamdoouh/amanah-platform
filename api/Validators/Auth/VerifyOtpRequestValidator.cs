using Amanah.Api.Services.Auth;
using Amanah.Contracts.Errors;
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
            .WithErrorCode(ErrorCodes.FieldPhoneRequired)
            .WithMessage("Phone number is required.")
            .Must(phone => PhoneNormalizer.TryNormalize(phone, out _))
            .WithErrorCode(ErrorCodes.FieldPhoneInvalid)
            .WithMessage("Phone number format is not valid.");

        RuleFor(request => request.Code)
            .NotEmpty()
            .WithErrorCode(ErrorCodes.FieldOtpCodeRequired)
            .WithMessage("Verification code is required.")
            .Must(code => OtpCodeNormalizer.TryNormalize(code, out _))
            .WithErrorCode(ErrorCodes.FieldOtpCodeInvalid)
            .WithMessage("Verification code format is not valid.");

        RuleFor(request => request.Purpose)
            .NotEmpty()
            .WithErrorCode(ErrorCodes.FieldOtpPurposeRequired)
            .WithMessage("OTP purpose is required.")
            .Must(AllowedPurposes.Contains)
            .WithErrorCode(ErrorCodes.FieldOtpPurposeInvalid)
            .WithMessage("OTP purpose is not valid.");
    }
}
