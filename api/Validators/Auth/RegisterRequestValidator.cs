using Amanah.Api.Utilities.Auth;
using Amanah.Contracts.Requests.Auth;
using FluentValidation;

namespace Amanah.Api.Validators.Auth;

public sealed class RegisterRequestValidator : AbstractValidator<RegisterRequest>
{
    public RegisterRequestValidator()
    {
        RuleFor(request => request.SignupToken)
            .NotEmpty()
            .WithMessage("Signup token is required.");

        RuleFor(request => request.DisplayName)
            .NotEmpty()
            .WithMessage("Display name is required.")
            .Must(DisplayNameValidator.IsValid)
            .WithMessage("Display name must be 3 to 40 characters using letters, numbers, spaces, or - _ .");

        RuleFor(request => request.Password)
            .NotEmpty()
            .WithMessage("Password is required.")
            .MinimumLength(PasswordRules.MinLength)
            .WithMessage($"Password must be at least {PasswordRules.MinLength} characters.");

        RuleFor(request => request.AcceptTerms)
            .Equal(true)
            .WithMessage("You must accept the terms and conditions and privacy policy.");
    }
}
