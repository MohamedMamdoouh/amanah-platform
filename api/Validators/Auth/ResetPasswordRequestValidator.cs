using Amanah.Api.Utilities.Auth;
using Amanah.Contracts.Errors;
using Amanah.Contracts.Requests.Auth;
using FluentValidation;

namespace Amanah.Api.Validators.Auth;

public sealed class ResetPasswordRequestValidator : AbstractValidator<ResetPasswordRequest>
{
    public ResetPasswordRequestValidator()
    {
        RuleFor(request => request.ResetToken)
            .NotEmpty()
            .WithErrorCode(ErrorCodes.FieldResetTokenRequired)
            .WithMessage("Reset token is required.");

        RuleFor(request => request.Password)
            .NotEmpty()
            .WithErrorCode(ErrorCodes.FieldPasswordRequired)
            .WithMessage("Password is required.")
            .MinimumLength(PasswordRules.MinLength)
            .WithErrorCode(ErrorCodes.FieldPasswordTooShort)
            .WithMessage($"Password must be at least {PasswordRules.MinLength} characters.");
    }
}
