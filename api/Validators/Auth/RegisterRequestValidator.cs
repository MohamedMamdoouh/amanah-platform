using Amanah.Api.Utilities.Auth;
using Amanah.Contracts.Errors;
using Amanah.Contracts.Requests.Auth;
using FluentValidation;

namespace Amanah.Api.Validators.Auth;

public sealed class RegisterRequestValidator : AbstractValidator<RegisterRequest>
{
    public RegisterRequestValidator()
    {
        RuleFor(request => request.SignupToken)
            .NotEmpty()
            .WithErrorCode(ErrorCodes.FieldSignupTokenRequired)
            .WithMessage("Signup token is required.");

        RuleFor(request => request.DisplayName)
            .NotEmpty()
            .WithErrorCode(ErrorCodes.FieldDisplayNameRequired)
            .WithMessage("Display name is required.")
            .Must(DisplayNameValidator.IsValid)
            .WithErrorCode(ErrorCodes.FieldDisplayNameInvalid)
            .WithMessage("Display name must be 3 to 40 characters using letters, numbers, spaces, or - _ .");

        RuleFor(request => request.Password)
            .NotEmpty()
            .WithErrorCode(ErrorCodes.FieldPasswordRequired)
            .WithMessage("Password is required.")
            .MinimumLength(PasswordRules.MinLength)
            .WithErrorCode(ErrorCodes.FieldPasswordTooShort)
            .WithMessage($"Password must be at least {PasswordRules.MinLength} characters.");

        RuleFor(request => request.AcceptTerms)
            .Equal(true)
            .WithErrorCode(ErrorCodes.FieldAcceptTermsRequired)
            .WithMessage("You must accept the terms and conditions and privacy policy.");
    }
}
