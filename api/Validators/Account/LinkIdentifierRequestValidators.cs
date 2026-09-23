using Amanah.Api.Services.Auth;
using Amanah.Contracts.Requests.Account;
using FluentValidation;

namespace Amanah.Api.Validators.Account;

public sealed class SendLinkIdentifierOtpRequestValidator : AbstractValidator<SendLinkIdentifierOtpRequest>
{
    public SendLinkIdentifierOtpRequestValidator()
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
    }

    private static bool IsChannel(SendLinkIdentifierOtpRequest request, AuthIdentifierChannel expected) =>
        AuthIdentifierNormalizer.TryParseChannel(request.Channel, out var parsed)
        && parsed == expected;
}

public sealed class VerifyLinkIdentifierOtpRequestValidator : AbstractValidator<VerifyLinkIdentifierOtpRequest>
{
    public VerifyLinkIdentifierOtpRequestValidator()
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

        RuleFor(request => request.Code)
            .NotEmpty()
            .WithMessage("Verification code is required.")
            .Must(code => OtpCodeNormalizer.TryNormalize(code, out _))
            .WithMessage("Verification code format is not valid.");
    }

    private static bool IsChannel(VerifyLinkIdentifierOtpRequest request, AuthIdentifierChannel expected) =>
        AuthIdentifierNormalizer.TryParseChannel(request.Channel, out var parsed)
        && parsed == expected;
}
