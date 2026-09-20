using Amanah.Api.Utilities.Auth;
using Amanah.Contracts.Requests.Auth;
using FluentValidation;

namespace Amanah.Api.Validators.Auth;

public sealed class ResetPasswordRequestValidator : AbstractValidator<ResetPasswordRequest>
{
    public ResetPasswordRequestValidator()
    {
        RuleFor(request => request.ResetToken)
            .NotEmpty()
            .WithMessage("Reset token is required.");

        RuleFor(request => request.Password)
            .NotEmpty()
            .WithMessage("Password is required.")
            .MinimumLength(PasswordRules.MinLength)
            .WithMessage($"Password must be at least {PasswordRules.MinLength} characters.");
    }
}
