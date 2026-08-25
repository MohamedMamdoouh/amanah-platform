using Amanah.Api.Models.Auth;
using Amanah.Api.Utilities;
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
            .WithMessage("Display name must be 3-40 characters using letters, digits, spaces, or - _ .");

        RuleFor(request => request.AcceptTerms)
            .Equal(true)
            .WithMessage("You must accept the Terms of Service and Privacy Policy.");
    }
}
