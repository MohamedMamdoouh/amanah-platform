using Amanah.Api.Utilities.Abuse;
using Amanah.Contracts.Requests.Abuse;
using FluentValidation;

namespace Amanah.Api.Validators.Abuse;

public sealed class FlagListingRequestValidator : AbstractValidator<FlagListingRequest>
{
    public FlagListingRequestValidator()
    {
        RuleFor(request => request.Reason)
            .NotEmpty()
            .WithMessage("Flag reason is required.")
            .Must(AbuseFlagReasons.All.Contains)
            .WithMessage("Flag reason is invalid.");

        RuleFor(request => request.Note)
            .MaximumLength(500)
            .When(request => request.Note is not null)
            .WithMessage("Flag note must be at most 500 characters.");
    }
}
