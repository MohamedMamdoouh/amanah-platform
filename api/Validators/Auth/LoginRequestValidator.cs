using Amanah.Api.Services.Auth;
using Amanah.Contracts.Requests.Auth;
using FluentValidation;

namespace Amanah.Api.Validators.Auth;

public sealed class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(request => request.Phone)
            .NotEmpty()
            .WithMessage("Phone number is required.")
            .Must(phone => PhoneNormalizer.TryNormalize(phone, out _))
            .WithMessage("Phone number format is not valid.");

        RuleFor(request => request.Password)
            .NotEmpty()
            .WithMessage("Password is required.");
    }
}
