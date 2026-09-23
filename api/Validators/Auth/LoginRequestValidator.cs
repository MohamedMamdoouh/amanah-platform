using Amanah.Api.Services.Auth;
using Amanah.Contracts.Requests.Auth;
using FluentValidation;

namespace Amanah.Api.Validators.Auth;

public sealed class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
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

        RuleFor(request => request.Password)
            .NotEmpty()
            .WithMessage("Password is required.");
    }

    private static bool IsChannel(LoginRequest request, AuthIdentifierChannel expected) =>
        AuthIdentifierNormalizer.TryParseChannel(request.Channel, out var parsed)
        && parsed == expected;
}
