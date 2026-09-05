using Amanah.Api.Utilities.Reports;
using Amanah.Contracts.Requests.Admin;
using FluentValidation;

namespace Amanah.Api.Validators.Admin;

public sealed class RejectReportRequestValidator : AbstractValidator<RejectReportRequest>
{
    public RejectReportRequestValidator()
    {
        RuleFor(request => request.ReasonCode)
            .NotEmpty()
            .WithMessage("Rejection reason is required.")
            .Must(RejectionReasonCodes.All.Contains)
            .WithMessage("Rejection reason is invalid.");

        RuleFor(request => request.Note)
            .MaximumLength(500)
            .When(request => request.Note is not null)
            .WithMessage("Rejection note must be at most 500 characters.");
    }
}
