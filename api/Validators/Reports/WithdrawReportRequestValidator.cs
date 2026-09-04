using Amanah.Contracts.Requests.Reports;
using FluentValidation;

namespace Amanah.Api.Validators.Reports;

public sealed class WithdrawReportRequestValidator : AbstractValidator<WithdrawReportRequest>
{
    private static readonly string[] AllowedReasons =
    [
        "recovered_outside",
        "no_longer_needed",
        "posted_by_mistake",
        "other",
    ];

    public WithdrawReportRequestValidator()
    {
        RuleFor(request => request.Reason)
            .Must(reason => reason is null || AllowedReasons.Contains(reason, StringComparer.Ordinal))
            .WithMessage("Withdrawal reason is invalid.");
    }
}
