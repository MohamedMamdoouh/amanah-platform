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
        RuleFor(request => request.Channel)
            .NotEmpty()
            .WithMessage("Sign-in channel is required.")
            .Must(channel => AuthIdentifierNormalizer.TryParseChannel(channel, out _))
            .WithMessage("Sign-in channel must be phone or email.");

        When(request => IsChannel(request, AuthIdentifierChannel.Phone), () =>
        {
            RuleFor(request => request.Identifier)
                .NotEmpty()
                .WithMessage("Phone number is required.")
                .Must(identifier => PhoneNormalizer.TryNormalize(identifier, out _))
                .WithMessage("Phone number format is not valid.");
        });

        When(request => IsChannel(request, AuthIdentifierChannel.Email), () =>
        {
            RuleFor(request => request.Identifier)
                .NotEmpty()
                .WithMessage("Email address is required.")
                .Must(identifier => EmailNormalizer.TryNormalize(identifier, out _))
                .WithMessage("Email format is not valid.");
        });

        RuleFor(request => request.CaptchaToken)
            .NotEmpty()
            .WithMessage("Captcha verification is required.");

        RuleFor(request => request.Purpose)
            .NotEmpty()
            .WithMessage("OTP purpose is required.")
            .Must(AllowedPurposes.Contains)
            .WithMessage("OTP purpose is not valid.");
    }

    private static bool IsChannel(SendOtpRequest request, AuthIdentifierChannel expected) =>
        AuthIdentifierNormalizer.TryParseChannel(request.Channel, out var parsed)
        && parsed == expected;
}
